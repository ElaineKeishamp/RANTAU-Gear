\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(ok boolean,label text) returns void language plpgsql as $$ begin if ok is distinct from true then raise exception 'FAIL: %',label; end if; raise notice 'PASS: %',label; end $$;
create function pg_temp.expect_error(sql text,expected text,label text) returns void language plpgsql as $$ begin begin execute sql; exception when others then if sqlerrm<>expected and sqlstate<>expected then raise exception 'FAIL: %, got %: %',label,sqlstate,sqlerrm; end if; raise notice 'PASS: %',label; return; end; raise exception 'FAIL: % succeeded',label; end $$;
insert into auth.users(id) values ('55555555-0000-0000-0000-000000000001'),('55555555-0000-0000-0000-000000000002');
insert into public.staff_profiles(id,display_name,role) values ('55555555-0000-0000-0000-000000000001','Pickup Front','front_office'),('55555555-0000-0000-0000-000000000002','Pickup QC','warehouse_qc');
insert into public.categories(name) values ('Pickup category') returning id as cat \gset
insert into public.equipment_types(category_id,name,daily_rate,replacement_value) values (:cat,'Pickup tent',100000,0) returning id as typ \gset
insert into public.equipment_units(equipment_type_id,code) values (:typ,'PICK-01') returning id as unit \gset
insert into public.equipment_units(equipment_type_id,code) values (:typ,'PICK-02') returning id as spare \gset
insert into public.equipment_units(equipment_type_id,code,state) values (:typ,'PICK-03','maintenance') returning id as maintenance \gset
insert into public.customers(name,phone) values ('Pickup customer','TEST') returning id as customer \gset
select (statement_timestamp()-interval '5 minutes')::text as pickup,(statement_timestamp()+interval '23 hours 55 minutes')::text as due \gset
select jsonb_build_array(jsonb_build_object('type_id',:typ,'quantity',1))::text as lines \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','55555555-0000-0000-0000-000000000001',true);
select (public.create_reservation(gen_random_uuid(),:customer,:'pickup',:'due',:'lines','cash','test',null)).id as rental \gset
select public.get_pickup_details(:rental)::text as details \gset
select pg_temp.assert_true((:'details'::jsonb->>'rent_balance')::numeric=75000,'Pickup shows remaining rent after DP');
select pg_temp.assert_true((:'details'::jsonb->>'deposit_balance')::numeric=100000,'Pickup shows separate refundable deposit');
select pg_temp.assert_true(jsonb_array_length(:'details'::jsonb->'units')=2,'Only ready units appear, maintenance excluded');
select pg_temp.assert_true((:'details'::jsonb->'lines'->0->>'quantity')::int=1,'Pickup lines preserve reserved quantity');
reset role;
update public.equipment_types set daily_rate=999999 where id=:typ;
set local role authenticated;
select pg_temp.assert_true((public.get_pickup_details(:rental)->>'rent_balance')::numeric=75000,'New master tariff does not change reserved balance');
select pg_temp.expect_error(format('select public.pickup_reservation(gen_random_uuid(),%s,array[%s,%s]::bigint[],''cash'','''')',:rental,:unit,:spare),'Jenis dan jumlah unit harus sama dengan reservasi.','Wrong selected quantity is rejected');
select pg_temp.assert_true((select count(*)=1 from public.payments where rental_id=:rental),'Rejected pickup creates no extra payment');
select pg_temp.expect_error(format('select public.pickup_reservation(gen_random_uuid(),%s,array[%s]::bigint[],''cash'','''')',:rental,:maintenance),'Semua unit harus siap, lolos QC, dan belum dipinjam.','Maintenance unit cannot be assigned');
select (public.pickup_reservation('55555555-5555-5555-5555-555555555555',:rental,array[:unit]::bigint[],'transfer','Pickup transfer')).id as result \gset
select pg_temp.assert_true((select state='active' from public.rentals where id=:rental),'Pickup activates rental');
select pg_temp.assert_true((select state='rented' from public.equipment_units where id=:unit),'Selected unit becomes rented');
select pg_temp.assert_true((select count(*)=1 from public.unit_assignments where rental_id=:rental and unit_id=:unit and released_at is null),'Pickup assigns exact physical unit');
select pg_temp.assert_true((select count(*)=1 and sum(amount)=75000 from public.payments where rental_id=:rental and kind='rent_payment' and method='transfer'),'Remaining rent recorded once at original snapshot');
select pg_temp.assert_true((select count(*)=1 and sum(amount)=100000 from public.payments where rental_id=:rental and kind='deposit_received'),'Deposit recorded separately once');
select pg_temp.assert_true((select rent_paid=100000 and deposit_received=100000 and dp_received=25000 from public.reservation_register where id=:rental),'Register distinguishes rent, deposit and prior DP');
select pg_temp.assert_true((public.pickup_reservation('55555555-5555-5555-5555-555555555555',:rental,array[:unit]::bigint[],'transfer','Pickup transfer')).id=:rental,'Same pickup UUID returns original rental');
select pg_temp.assert_true((select count(*)=3 from public.payments where rental_id=:rental),'Retry cannot duplicate rent or deposit');
select pg_temp.expect_error(format('select public.get_pickup_details(%s)',:rental),'RG_PICKUP_STATE','Already active rental cannot open new pickup');
select set_config('request.jwt.claim.sub','55555555-0000-0000-0000-000000000002',true);
select pg_temp.expect_error(format('select public.get_pickup_details(%s)',:rental),'42501','QC cannot read pickup customer balances');
select pg_temp.expect_error(format('select public.pickup_reservation(gen_random_uuid(),%s,array[%s]::bigint[],''cash'','''')',:rental,:unit),'42501','QC cannot receive deposit');
reset role;
select pg_temp.assert_true(not has_function_privilege('anon','public.get_pickup_details(bigint)','execute'),'Anonymous pickup details denied');
rollback;
