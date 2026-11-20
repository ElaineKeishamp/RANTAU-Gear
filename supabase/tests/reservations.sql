\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(p_ok boolean,p_label text) returns void language plpgsql as $$
begin
    if p_ok is distinct from true then raise exception 'FAIL: %',p_label; end if;
    raise notice 'PASS: %',p_label;
end;
$$;
create function pg_temp.expect_error(p_sql text,p_state text,p_label text) returns void language plpgsql as $$
begin
    begin
        execute p_sql;
    exception when others then
        if sqlstate <> p_state then raise exception 'FAIL: %, expected %, got %: %',p_label,p_state,sqlstate,sqlerrm; end if;
        raise notice 'PASS: %',p_label;
        return;
    end;
    raise exception 'FAIL: % (operation succeeded)',p_label;
end;
$$;
insert into auth.users(id) values ('00000000-0000-0000-0000-000000000001'),('00000000-0000-0000-0000-000000000002'),('00000000-0000-0000-0000-000000000003');
insert into public.staff_profiles(id,display_name,role) values
('00000000-0000-0000-0000-000000000001','Test Admin','super_admin'),
('00000000-0000-0000-0000-000000000002','Test Front','front_office'),
('00000000-0000-0000-0000-000000000003','Test QC','warehouse_qc');
insert into public.categories(name) values ('Test Tenda') returning id as category_id \gset
insert into public.equipment_types(category_id,name,daily_rate,replacement_value)
values (:category_id,'Test Dome 4P',100000,1000000) returning id as type_id \gset
insert into public.equipment_units(equipment_type_id,code) values (:type_id,'TEST-001'),(:type_id,'TEST-002');
insert into public.equipment_units(equipment_type_id,code,state) values (:type_id,'TEST-MAINT','maintenance'),(:type_id,'TEST-LOST','lost');
insert into public.customers(name,phone) values ('Test Customer','080000000') returning id as customer_id \gset
select now()+interval '2 days' as pickup, now()+interval '4 days' as due \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000002',true);
select pg_temp.assert_true(public.check_availability(:type_id,:'pickup',:'due')=2,'Maintenance and lost units excluded');
select (public.create_reservation('11111111-1111-1111-1111-111111111111',:customer_id,:'pickup',:'due',
    jsonb_build_array(jsonb_build_object('type_id',:type_id,'quantity',2)),'cash')).id as rental_id \gset
select pg_temp.assert_true((select rent_amount=400000 and down_payment_due=100000 and deposit_due=400000 and rental_days=2 from public.rentals where id=:rental_id),'Two units, two days, 25 percent DP and 100 percent deposit');
select pg_temp.assert_true((select sum(amount)=100000 from public.payments where rental_id=:rental_id and kind='down_payment'),'DP payment recorded atomically');
select pg_temp.assert_true(public.check_availability(:type_id,:'pickup',:'due')=0,'Reservation consumes quantity without assigning physical unit');
select pg_temp.assert_true((select count(*)=0 from public.unit_assignments where rental_id=:rental_id),'Unit selection deferred until pickup');
select pg_temp.assert_true((public.create_reservation('11111111-1111-1111-1111-111111111111',:customer_id,:'pickup',:'due',
    jsonb_build_array(jsonb_build_object('type_id',:type_id,'quantity',2)),'cash')).id=:rental_id,'Retry returns same reservation');
select pg_temp.assert_true((select count(*)=1 from public.payments where rental_id=:rental_id),'Retry does not duplicate money');
select pg_temp.expect_error(format('select public.create_reservation(%L,%s,%L,%L,%L::jsonb,%L)',
    '22222222-2222-2222-2222-222222222222',:customer_id,:'pickup',:'due',
    jsonb_build_array(jsonb_build_object('type_id',:type_id,'quantity',1))::text,'cash'),'P0001','Overlapping over-capacity reservation rejected');
select pg_temp.expect_error(format('select public.create_reservation(%L,%s,%L,%L,%L::jsonb,%L)',
    '11111111-1111-1111-1111-111111111111',:customer_id,:'pickup',:'due',
    jsonb_build_array(jsonb_build_object('type_id',:type_id,'quantity',1))::text,'cash'),'22023','Request ID cannot be reused with changed payload');
select pg_temp.assert_true(public.check_availability(:type_id,:'due'::timestamptz+interval '119 minutes',:'due'::timestamptz+interval '1 day')=0,'Two-hour buffer blocks early reuse');
select pg_temp.assert_true(public.check_availability(:type_id,:'due'::timestamptz+interval '2 hours',:'due'::timestamptz+interval '1 day')=2,'Half-open boundary releases stock at buffer end');
select pg_temp.assert_true(public.calculate_late_fine(:rental_id,:'due'::timestamptz+interval '1 hour')=0,'One-hour grace includes exact boundary');
select pg_temp.assert_true(public.calculate_late_fine(:rental_id,:'due'::timestamptz+interval '1 hour 1 second')=50000,'First late day costs 25 percent of combined daily price');
select pg_temp.assert_true(public.calculate_late_fine(:rental_id,:'due'::timestamptz+interval '25 hours')=50000,'Exact first 24 hours after grace is one day');
select pg_temp.assert_true(public.calculate_late_fine(:rental_id,:'due'::timestamptz+interval '25 hours 1 second')=100000,'Next late day rounds upward');
select pg_temp.expect_error('update public.staff_profiles set role=''super_admin'' where id=auth.uid()','42501','Staff cannot escalate own role');
select pg_temp.expect_error(format('delete from public.rentals where id=%s',:rental_id),'42501','Staff cannot erase a paid reservation');
select pg_temp.expect_error(format('update public.payments set amount=1 where rental_id=%s',:rental_id),'42501','Staff cannot overwrite payments');
select pg_temp.assert_true((public.cancel_reservation(:rental_id,'Pelanggan membatalkan')).state='cancelled','Cancellation keeps rental record');
select pg_temp.assert_true((select sum(amount)=100000 from public.payments where rental_id=:rental_id),'Cancellation retains nonrefundable DP');
select pg_temp.assert_true(public.check_availability(:type_id,:'pickup',:'due')=2,'Cancellation releases capacity');
select pg_temp.assert_true((select count(*)=2 from public.rental_events where rental_id=:rental_id),'Reservation and cancellation events retained');
reset role;
update public.rental_settings set late_percent_per_day=50 where id=1;
set local role authenticated;
select pg_temp.assert_true(public.calculate_late_fine(:rental_id,:'due'::timestamptz+interval '1 hour 1 second')=50000,'New setting does not rewrite old agreed rate');
select (public.create_reservation('33333333-3333-3333-3333-333333333333',:customer_id,:'pickup',:'due',
    jsonb_build_array(jsonb_build_object('type_id',:type_id,'quantity',1)),'transfer','test')).id as expired_id \gset
reset role;
update public.rentals set pickup_at=now()-interval '13 hours',pickup_deadline_at=now()-interval '1 hour' where id=:expired_id;
set local role authenticated;
select pg_temp.assert_true(public.check_availability(:type_id,:'pickup',:'due')=2,'Expired reservation releases capacity even before sweep');
select pg_temp.assert_true((select display_state='expired' from public.rental_overview where id=:expired_id),'Overview shows expired status before sweep');
select pg_temp.assert_true(public.expire_reservations()=1,'No-show sweep records expiration');
select pg_temp.assert_true((select state='expired' from public.rentals where id=:expired_id),'Expired rental remains in history');
select pg_temp.assert_true((select count(*)=1 from public.payments where rental_id=:expired_id),'Expiration preserves DP payment');
select now()-interval '1 minute' as walkin_pickup,now()+interval '23 hours 59 minutes' as walkin_due \gset
select (public.create_reservation('44444444-4444-4444-4444-444444444444',:customer_id,:'walkin_pickup',:'walkin_due',
    jsonb_build_array(jsonb_build_object('type_id',:type_id,'quantity',2)),'cash')).id as walkin_id \gset
select array_agg(id order by id)::text as selected_units from public.equipment_units where equipment_type_id=:type_id and state='ready' \gset
select pg_temp.assert_true((public.pickup_reservation('55555555-5555-5555-5555-555555555555',:walkin_id,:'selected_units'::bigint[],'cash')).state='active','Walk-in follows reservation then pickup');
select pg_temp.assert_true((select sum(amount)=150000 from public.payments where rental_id=:walkin_id and kind='rent_payment'),'Pickup receives remaining rent after DP');
select pg_temp.assert_true((select sum(amount)=200000 from public.payments where rental_id=:walkin_id and kind='deposit_received'),'Pickup receives deposit separately');
select pg_temp.assert_true((select count(*)=2 from public.unit_assignments where rental_id=:walkin_id and released_at is null),'Pickup assigns every physical unit');
select pg_temp.assert_true((public.pickup_reservation('55555555-5555-5555-5555-555555555555',:walkin_id,:'selected_units'::bigint[],'cash')).id=:walkin_id,'Pickup retry is idempotent');
select pg_temp.assert_true((select count(*)=3 from public.payments where rental_id=:walkin_id),'Pickup retry does not duplicate rent or deposit');
select (public.extend_rental('66666666-6666-6666-6666-666666666666',:walkin_id,:'walkin_due'::timestamptz+interval '1 day','transfer')).id;
select pg_temp.assert_true((select rent_amount=400000 and deposit_due=200000 from public.rentals where id=:walkin_id),'Extension adds rent and retains existing deposit');
select pg_temp.assert_true((select sum(amount)=350000 from public.payments where rental_id=:walkin_id and kind='rent_payment'),'Extension payment recorded');
select id as good_unit from public.equipment_units where code='TEST-001' \gset
select id as lost_unit from public.equipment_units where code='TEST-002' \gset
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000003',true);
select pg_temp.expect_error(format('select public.inspect_return(%L,%s,%L::jsonb)',
    '77777777-7777-7777-7777-777777777777',:walkin_id,jsonb_build_array(jsonb_build_object('unit_id',:good_unit,'condition','good','charge',0))::text),
    'P0001','Partial return rejected before writing any inspection');
select pg_temp.assert_true((select count(*)=0 from public.inspections),'Rejected partial return leaves no partial data');
select pg_temp.assert_true((public.inspect_return('77777777-7777-7777-7777-777777777777',:walkin_id,
    jsonb_build_array(jsonb_build_object('unit_id',:good_unit,'condition','good','charge',0),
        jsonb_build_object('unit_id',:lost_unit,'condition','lost','charge',1000000,'notes','Tas tenda hilang')))).state='returned','Return includes missing unit as loss');
select pg_temp.assert_true((select state='lost' from public.equipment_units where id=:lost_unit),'Lost unit removed from rentable inventory');
select pg_temp.expect_error(format('select public.approve_return_charges(%L,%s)','88888888-8888-8888-8888-888888888888',:walkin_id),'42501','QC cannot approve own damage or loss charge');
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000001',true);
select (public.approve_return_charges('88888888-8888-8888-8888-888888888888',:walkin_id)).id;
select pg_temp.assert_true((select sum(amount)=1000000 from public.fines where rental_id=:walkin_id),'Admin approves full loss charge beyond deposit');
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000002',true);
select (public.settle_return('99999999-9999-9999-9999-999999999999',:walkin_id,'cash')).id;
select pg_temp.assert_true((select sum(d.amount)=200000 from public.deposit_deductions d join public.fines f on f.id=d.fine_id where f.rental_id=:walkin_id),'Deposit offsets loss charge');
select pg_temp.assert_true((select sum(amount)=800000 from public.payments where rental_id=:walkin_id and kind='fine_payment'),'Customer pays loss charge exceeding deposit');
select (public.settle_return('99999999-9999-9999-9999-999999999999',:walkin_id,'cash')).id;
select pg_temp.assert_true((select count(*)=1 from public.payments where rental_id=:walkin_id and kind='fine_payment'),'Settlement retry does not duplicate payment');
select pg_temp.assert_true(public.check_availability(:type_id,now()+interval '3 hours',now()+interval '1 day')=1,'After return buffer only surviving unit becomes available');
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000003',true);
select pg_temp.assert_true((select count(*)=0 from public.customers),'QC cannot read customer contacts');
select pg_temp.assert_true((select count(*)=0 from public.payments),'QC cannot read payment ledger');
select pg_temp.expect_error('select public.save_customer(null,''Unauthorized'',''08'')','42501','QC cannot create customers');
reset role;
insert into public.equipment_types(category_id,name,daily_rate,replacement_value) values (:category_id,'Refund Test Tent',100000,1000000) returning id as refund_type \gset
insert into public.equipment_units(equipment_type_id,code) values (:refund_type,'REFUND-001'),(:refund_type,'REFUND-002'),(:refund_type,'REFUND-003');
insert into public.packages(name,daily_price) values ('Test Discount Package',80000) returning id as package_id \gset
insert into public.package_components(package_id,equipment_type_id,quantity) values (:package_id,:refund_type,2);
select id as refund_good from public.equipment_units where code='REFUND-001' \gset
select id as refund_damage from public.equipment_units where code='REFUND-002' \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000002',true);
select (public.create_reservation('a1111111-1111-1111-1111-111111111111',:customer_id,:'walkin_pickup',:'walkin_due',
    jsonb_build_array(jsonb_build_object('type_id',:refund_type,'quantity',2)),'cash','',:package_id)).id as refund_rental \gset
select pg_temp.assert_true((select daily_price=80000 and rent_amount=80000 and deposit_due=80000 and down_payment_due=20000 from public.rentals where id=:refund_rental),'Package price drives rent, DP and deposit instead of component sum');
select pg_temp.assert_true(public.calculate_late_fine(:refund_rental,:'walkin_due'::timestamptz+interval '1 hour 1 second')=40000,'Package late fine uses snapshotted package rate');
select (public.pickup_reservation('a2222222-2222-2222-2222-222222222222',:refund_rental,array[:refund_good,:refund_damage],'cash')).id;
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000003',true);
select (public.inspect_return('a3333333-3333-3333-3333-333333333333',:refund_rental,
    jsonb_build_array(jsonb_build_object('unit_id',:refund_good,'condition','good','charge',0),
        jsonb_build_object('unit_id',:refund_damage,'condition','damaged','charge',20000,'notes','Tiang rusak')))).id;
select pg_temp.assert_true((select state='maintenance' from public.equipment_units where id=:refund_damage),'Damaged unit enters maintenance');
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000001',true);
select (public.approve_return_charges('a4444444-4444-4444-4444-444444444444',:refund_rental)).id;
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000002',true);
select (public.settle_return('a5555555-5555-5555-5555-555555555555',:refund_rental,'transfer')).id;
select pg_temp.assert_true((select sum(amount)=60000 from public.payments where rental_id=:refund_rental and kind='deposit_refund'),'Unused deposit refunded after approved damage charge');
select pg_temp.assert_true((select count(*)=0 from public.payments where rental_id=:refund_rental and kind='fine_payment'),'No extra payment when deposit covers all fines');
select pg_temp.assert_true(public.check_availability(:refund_type,now()+interval '3 hours',now()+interval '1 day')=2,'Damaged unit excluded without subtracting it twice during return buffer');
select now()+interval '10 days' as first_start,now()+interval '11 days' as first_end,
    now()+interval '11 days 2 hours' as second_start,now()+interval '12 days 2 hours' as second_end \gset
select (public.create_reservation('a6666666-6666-6666-6666-666666666666',:customer_id,:'first_start',:'first_end',
    jsonb_build_array(jsonb_build_object('type_id',:refund_type,'quantity',1)),'cash')).id;
select (public.create_reservation('a7777777-7777-7777-7777-777777777777',:customer_id,:'second_start',:'second_end',
    jsonb_build_array(jsonb_build_object('type_id',:refund_type,'quantity',1)),'cash')).id;
select pg_temp.assert_true(public.check_availability(:refund_type,:'first_start',:'second_end')=1,'Availability counts peak simultaneous demand, not sum of all intersecting bookings');
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000003',true);
reset role;
update public.staff_profiles set is_active=false where id='00000000-0000-0000-0000-000000000003';
set local role authenticated;
select pg_temp.assert_true((select count(*)=0 from public.rentals),'Inactive staff loses rental visibility');
select pg_temp.expect_error(format('select public.check_availability(%s,%L,%L)',:type_id,:'pickup',:'due'),'42501','Inactive staff cannot call RPC');
reset role;
set local role anon;
select pg_temp.expect_error('select public.expire_reservations()','42501','Anonymous caller cannot invoke business RPC');
select pg_temp.expect_error('select * from public.rentals','42501','Anonymous caller cannot read rentals');
select pg_temp.expect_error('select * from public.rental_overview','42501','Overview does not bypass anonymous access restrictions');
select pg_temp.expect_error('select nextval(''public.rental_number_seq'')','42501','Anonymous caller cannot consume rental numbers');
select pg_temp.expect_error('select rantau_private.is_staff(array[''super_admin'']::public.staff_role[])','42501','Anonymous caller cannot access private helpers');
reset role;
select pg_temp.assert_true((select count(*)>0 from public.audit_log where table_name='payments'),'Payment audit captured');
select pg_temp.expect_error('delete from public.payments','23514','Database trigger protects payment history even with elevated SQL');
rollback;
