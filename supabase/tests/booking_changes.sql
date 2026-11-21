\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(ok boolean,label text) returns void language plpgsql as $$ begin if ok is distinct from true then raise exception 'FAIL: %',label; end if; raise notice 'PASS: %',label; end $$;
create function pg_temp.expect_error(sql text,expected text,label text) returns void language plpgsql as $$ begin begin execute sql; exception when others then if sqlerrm<>expected and sqlstate<>expected then raise exception 'FAIL: %, got %: %',label,sqlstate,sqlerrm; end if; raise notice 'PASS: %',label; return; end; raise exception 'FAIL: % succeeded',label; end $$;
insert into auth.users(id) values ('77777777-0000-0000-0000-000000000001'),('77777777-0000-0000-0000-000000000002');
insert into public.staff_profiles(id,display_name,role) values ('77777777-0000-0000-0000-000000000001','Change Front','front_office'),('77777777-0000-0000-0000-000000000002','Change QC','warehouse_qc');
insert into public.categories(name) values ('Change category') returning id as cat \gset
insert into public.equipment_types(category_id,name,daily_rate,replacement_value) values (:cat,'Change tent',100000,0) returning id as typ \gset
insert into public.equipment_units(equipment_type_id,code) values (:typ,'CHANGE-01') returning id as unit \gset
insert into public.customers(name,phone) values ('Change customer','TEST') returning id as customer \gset
select jsonb_build_array(jsonb_build_object('type_id',:typ,'quantity',1))::text as lines \gset
select (statement_timestamp()-interval '5 minutes')::text as pickup,(statement_timestamp()+interval '23 hours 55 minutes')::text as due,(statement_timestamp()+interval '47 hours 55 minutes')::text as extended,(statement_timestamp()+interval '26 hours')::text as next_pickup,(statement_timestamp()+interval '50 hours')::text as next_due \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','77777777-0000-0000-0000-000000000001',true);
select (public.create_reservation(gen_random_uuid(),:customer,:'pickup',:'due',:'lines','cash','',null)).id as active \gset
select (public.pickup_reservation(gen_random_uuid(),:active,array[:unit]::bigint[],'cash','')).id;
select pg_temp.assert_true((public.get_booking_change_details(:active)->>'deposit_received')::numeric=100000,'Change details use received deposit ledger');
select pg_temp.assert_true((public.preview_extension(:active,:'extended')->>'additional')::numeric=100000,'Extension quote uses original daily snapshot');
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''extend'',%L,'''',%L,1,''cash'','''')',:active,:'extended',:'due'),'RG_CHANGE_QUOTE','Incorrect confirmed additional charge rejected');
select pg_temp.assert_true((select count(*)=3 from public.payments where rental_id=:active),'Rejected extension does not record money');
select (public.create_reservation(gen_random_uuid(),:customer,:'next_pickup',:'next_due',:'lines','cash','',null)).id as future \gset
select pg_temp.expect_error(format('select public.preview_extension(%s,%L)',:active,:'extended'),'RG_CHANGE_STOCK','Preview detects future reservation conflict');
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''extend'',%L,'''',%L,100000,''cash'','''')',:active,:'extended',:'due'),'RG_CHANGE_STOCK','Save rechecks conflict after preview');
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''cancel'',null,'''',null,null,''cash'','''')',:future),'RG_CHANGE_REASON','Cancellation requires reason');
select (public.change_booking('77777777-7777-7777-7777-777777777771',:future,'cancel',null,'Pelanggan membatalkan',null,null,'cash','')).id;
select (public.change_booking('77777777-7777-7777-7777-777777777771',:future,'cancel',null,'Pelanggan membatalkan',null,null,'cash','')).id;
select pg_temp.assert_true((select state='cancelled' and cancellation_reason='Pelanggan membatalkan' from public.rentals where id=:future),'Cancellation preserves rental and reason');
select pg_temp.assert_true((select count(*)=1 and sum(amount)=25000 from public.payments where rental_id=:future),'Cancellation preserves DP without refund');
select pg_temp.assert_true((select count(*)=1 from public.rental_events where rental_id=:future and event_type='cancelled'),'Cancellation retry creates one event');
select (public.change_booking('77777777-7777-7777-7777-777777777772',:active,'extend',:'extended','',:'due',100000,'transfer','Extension paid')).id;
select (public.change_booking('77777777-7777-7777-7777-777777777772',:active,'extend',:'extended','',:'due',100000,'transfer','Extension paid')).id;
select pg_temp.assert_true((select rent_amount=200000 and due_at=:'extended' from public.rentals where id=:active),'Extension updates total rent and due date');
select pg_temp.assert_true((select count(*)=4 from public.payments where rental_id=:active),'Extension retry charges only once');
select pg_temp.assert_true((select sum(amount)=100000 from public.payments where rental_id=:active and kind='deposit_received'),'Extension keeps original deposit');
select pg_temp.expect_error(format('select public.change_booking(''77777777-7777-7777-7777-777777777772'',%s,''extend'',%L,'''',%L,0,''transfer'',''Extension paid'')',:active,:'extended',:'due'),'RG_CHANGE_REQUEST','Same UUID cannot change confirmed amount');
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''cancel'',null,''Wrong state'',null,null,''cash'','''')',:active),'RG_CHANGE_STATE','Active rental cannot be cancelled');
reset role;
update public.rentals set due_at=statement_timestamp()-interval '1 minute' where id=:active;
set local role authenticated;
select pg_temp.expect_error(format('select public.preview_extension(%s,%L)',:active,:'extended'),'RG_CHANGE_LATE','Overdue extension cannot erase late fine');
reset role;
-- Fixture expired booking; its DP and history must survive expiry.
insert into public.rentals(customer_id,pickup_at,due_at,daily_price,rental_days,rent_amount,down_payment_due,deposit_due,late_percent_per_day,grace_minutes,buffer_minutes,pickup_deadline_at,created_by)
values (:customer,statement_timestamp()-interval '13 hours',statement_timestamp()+interval '11 hours',100000,1,100000,25000,100000,25,60,120,statement_timestamp()-interval '1 hour','77777777-0000-0000-0000-000000000001') returning id as expired \gset
insert into public.payments(rental_id,kind,amount,method,recorded_by) values (:expired,'down_payment',25000,'cash','77777777-0000-0000-0000-000000000001');
set local role authenticated;
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''cancel'',null,''Terlambat batal'',null,null,''cash'','''')',:expired),'RG_CHANGE_EXPIRED','Past-deadline booking uses expiry instead of cancellation');
select (public.change_booking('77777777-7777-7777-7777-777777777773',:expired,'expire',null,'',null,null,'cash','')).id;
select (public.change_booking('77777777-7777-7777-7777-777777777773',:expired,'expire',null,'',null,null,'cash','')).id;
select pg_temp.assert_true((select state='expired' from public.rentals where id=:expired),'Expiry records expired state');
select pg_temp.assert_true((select count(*)=1 from public.rental_events where rental_id=:expired and event_type='expired'),'Expiry retry creates one event');
select pg_temp.assert_true((select sum(amount)=25000 and count(*)=1 from public.payments where rental_id=:expired),'Expiry preserves DP and creates no refund');

reset role;
insert into public.equipment_units(equipment_type_id,code) values (:typ,'CHANGE-02') returning id as short_unit \gset
select (statement_timestamp()+interval '55 minutes')::text as short_due,(statement_timestamp()+interval '115 minutes')::text as short_extended \gset
set local role authenticated;
select (public.create_reservation(gen_random_uuid(),:customer,:'pickup',:'short_due',:'lines','cash','',null)).id as short_rental \gset
select (public.pickup_reservation(gen_random_uuid(),:short_rental,array[:short_unit]::bigint[],'cash','')).id;
select pg_temp.assert_true((public.preview_extension(:short_rental,:'short_extended')->>'additional')::numeric=0,'Extension within billed day has zero extra fee');
select (public.change_booking(gen_random_uuid(),:short_rental,'extend',:'short_extended','',:'short_due',0,'cash','No extra cash')).id;
select pg_temp.assert_true((select count(*)=3 from public.payments where rental_id=:short_rental),'Zero-cost extension creates no payment row');
select (public.create_reservation(gen_random_uuid(),:customer,:'next_pickup',:'next_due',:'lines','cash','',null)).id as not_expired \gset
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''expire'',null,'''',null,null,''cash'','''')',:not_expired),'RG_CHANGE_NOT_EXPIRED','Before deadline expiry is rejected');
select pg_temp.assert_true((select state='reserved' from public.rentals where id=:not_expired),'Early expiry attempt leaves reservation intact');

select set_config('request.jwt.claim.sub','77777777-0000-0000-0000-000000000002',true);
select pg_temp.expect_error(format('select public.get_booking_change_details(%s)',:active),'42501','QC cannot read customer change/payment details');
select pg_temp.expect_error(format('select public.change_booking(gen_random_uuid(),%s,''expire'',null,'''',null,null,''cash'','''')',:expired),'42501','QC cannot mutate reservation lifecycle');
reset role;
select pg_temp.assert_true(not has_function_privilege('anon','public.change_booking(uuid,bigint,text,timestamptz,text,timestamptz,numeric,public.payment_method,text)','execute'),'Anonymous booking changes denied');
rollback;
