\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(ok boolean,label text) returns void language plpgsql as $$ begin if ok is distinct from true then raise exception 'FAIL: %',label; end if; raise notice 'PASS: %',label; end $$;
create function pg_temp.expect_error(sql text,expected text,label text) returns void language plpgsql as $$ begin begin execute sql; exception when others then if sqlerrm<>expected and sqlstate<>expected then raise exception 'FAIL: %, got %: %',label,sqlstate,sqlerrm; end if; raise notice 'PASS: %',label; return; end; raise exception 'FAIL: % succeeded',label; end $$;
insert into auth.users(id) values ('22222222-0000-0000-0000-000000000001'),('22222222-0000-0000-0000-000000000002');
insert into public.staff_profiles(id,display_name,role) values ('22222222-0000-0000-0000-000000000001','Booking Front','front_office'),('22222222-0000-0000-0000-000000000002','Booking QC','warehouse_qc');
insert into public.categories(name) values ('Booking category') returning id as cat \gset
insert into public.equipment_types(category_id,name,daily_rate,replacement_value) values (:cat,'Booking tent',100001,0) returning id as typ \gset
insert into public.equipment_units(equipment_type_id,code) values (:typ,'BOOKING-01');
insert into public.customers(name,phone) values ('Booking customer','TEST') returning id as customer \gset
insert into public.packages(name,daily_price) values ('Booking package',80000) returning id as package \gset
insert into public.package_components(package_id,equipment_type_id,quantity) values (:package,:typ,1);
select jsonb_build_array(jsonb_build_object('type_id',:typ,'quantity',1))::text as lines \gset
select (statement_timestamp()+interval '2 days')::text as pickup,(statement_timestamp()+interval '3 days')::text as due,(statement_timestamp()+interval '3 days 1 minute')::text as longer \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','22222222-0000-0000-0000-000000000001',true);
select public.preview_reservation(:'pickup',:'due',:'lines',null)::text as quote \gset
select pg_temp.assert_true((:'quote'::jsonb->>'rent_amount')::numeric=100001,'Preview returns server rate');
select pg_temp.assert_true((:'quote'::jsonb->>'down_payment_due')::numeric=25001,'DP rounds up to full rupiah');
select pg_temp.assert_true((:'quote'::jsonb->>'deposit_due')::numeric=100001,'Deposit remains separate from DP');
select pg_temp.assert_true((public.preview_reservation(:'pickup',:'longer',:'lines',null)->>'rental_days')::int=2,'Extra minute creates second rental day');
select pg_temp.assert_true((public.preview_reservation(:'pickup',:'due',:'lines',:package)->>'rent_amount')::numeric=80000,'Package uses package price');
select pg_temp.expect_error(format('select public.reserve_with_dp(''22222222-2222-2222-2222-222222222222'',%s,%L,%L,%L,''cash'','''',null,1,1,1)',:customer,:'pickup',:'due',:'lines'),'RG_BOOKING_CHANGED','Changed quote rejects reservation and DP');
select pg_temp.assert_true((select count(*)=0 from public.rentals where customer_id=:customer),'Changed quote leaves no rental');
select pg_temp.assert_true((select count(*)=0 from public.payments),'Changed quote leaves no payment');
select (public.reserve_with_dp('22222222-2222-2222-2222-222222222222',:customer,:'pickup',:'due',:'lines','transfer','DP test',null,100001,25001,100001)).id as rental \gset
select pg_temp.assert_true((select state='reserved' and down_payment_due=25001 from public.rentals where id=:rental),'Front office records reservation');
select pg_temp.assert_true((select count(*)=1 and sum(amount)=25001 from public.payments where rental_id=:rental and kind='down_payment' and method='transfer'),'DP is recorded once with selected method');
select pg_temp.assert_true((select count(*)=0 from public.payments where rental_id=:rental and kind<>'down_payment'),'No deposit or rent balance received at reservation');
select pg_temp.assert_true((select dp_received=25001 and customer_name='Booking customer' from public.reservation_register where id=:rental),'Register shows customer and actual DP ledger');
select pg_temp.assert_true((public.reserve_with_dp('22222222-2222-2222-2222-222222222222',:customer,:'pickup',:'due',:'lines','transfer','DP test',null,100001,25001,100001)).id=:rental,'Retry same UUID returns same rental');
select pg_temp.assert_true((select count(*)=1 from public.payments where rental_id=:rental),'Retry cannot duplicate DP');
select pg_temp.expect_error(format('select public.preview_reservation(%L,%L,%L,null)',:'pickup',:'due',:'lines'),'RG_BOOKING_STOCK','Preview detects stock conflict');
select pg_temp.expect_error(format('select public.reserve_with_dp(''33333333-3333-3333-3333-333333333333'',%s,%L,%L,%L,''cash'','''',null,100001,25001,100001)',:customer,:'pickup',:'due',:'lines'),'RG_BOOKING_STOCK','Save rechecks capacity after prior preview');
select set_config('request.jwt.claim.sub','22222222-0000-0000-0000-000000000002',true);
select pg_temp.expect_error(format('select public.preview_reservation(%L,%L,%L,null)',:'pickup',:'due',:'lines'),'42501','QC cannot preview customer reservation');
select pg_temp.expect_error(format('select public.reserve_with_dp(''44444444-4444-4444-4444-444444444444'',%s,%L,%L,%L,''cash'','''',null,100001,25001,100001)',:customer,:'pickup',:'due',:'lines'),'42501','QC cannot receive DP');
select pg_temp.assert_true((select count(*)=0 from public.reservation_register),'QC cannot read customer payment register');
reset role;
select pg_temp.assert_true(not has_function_privilege('anon','public.reserve_with_dp(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint,numeric,numeric,numeric)','execute'),'Anonymous reservation RPC denied');
rollback;
