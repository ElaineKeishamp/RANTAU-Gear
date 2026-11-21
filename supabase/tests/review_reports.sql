\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(ok boolean,label text) returns void language plpgsql as $$ begin if ok is distinct from true then raise exception 'FAIL: %',label; end if; raise notice 'PASS: %',label; end $$;
create function pg_temp.expect_error(sql text,expected text,label text) returns void language plpgsql as $$ begin begin execute sql; exception when others then if sqlstate<>expected then raise exception 'FAIL: %, got %: %',label,sqlstate,sqlerrm; end if; raise notice 'PASS: %',label; return; end; raise exception 'FAIL: % succeeded',label; end $$;
insert into auth.users(id) values ('88888888-0000-0000-0000-000000000001'),('88888888-0000-0000-0000-000000000002');
insert into public.staff_profiles(id,display_name,role) values ('88888888-0000-0000-0000-000000000001','Report Front','front_office'),('88888888-0000-0000-0000-000000000002','Report QC','warehouse_qc');
select (statement_timestamp() at time zone 'Asia/Jakarta')::date::text as day \gset
select (:'day'::date::timestamp at time zone 'Asia/Jakarta')::text as starts,((:'day'::date+1)::timestamp at time zone 'Asia/Jakarta')::text as ends \gset
insert into public.customers(name,phone) values ('Report customer','TEST') returning id as customer \gset
insert into public.rentals(customer_id,state,pickup_at,due_at,daily_price,rental_days,rent_amount,down_payment_due,deposit_due,late_percent_per_day,grace_minutes,buffer_minutes,pickup_deadline_at,created_by)
values (:customer,'cancelled',:'starts',:'ends',100000,1,100000,25000,100000,25,60,120,:'starts'::timestamptz+interval '12 hours','88888888-0000-0000-0000-000000000001') returning id as rental \gset
insert into public.payments(rental_id,kind,amount,method,recorded_by,recorded_at) values
(:rental,'deposit_received',50000,'cash','88888888-0000-0000-0000-000000000001',:'starts'::timestamptz-interval '1 second'),
(:rental,'down_payment',25000,'cash','88888888-0000-0000-0000-000000000001',:'starts'),
(:rental,'rent_payment',75000,'transfer','88888888-0000-0000-0000-000000000001',:'starts'),
(:rental,'deposit_received',100000,'cash','88888888-0000-0000-0000-000000000001',:'starts'),
(:rental,'fine_payment',15000,'cash','88888888-0000-0000-0000-000000000001',:'starts'),
(:rental,'deposit_refund',75000,'cash','88888888-0000-0000-0000-000000000001',:'starts'),
(:rental,'rent_refund',5000,'cash','88888888-0000-0000-0000-000000000001',:'starts'),
(:rental,'deposit_refund',10000,'cash','88888888-0000-0000-0000-000000000001',:'ends');
-- A report-only fixture approver; operational role grants are tested separately.
insert into public.fines(rental_id,kind,amount,reason,approved_by) values(:rental,'late',25000,'Fixture','88888888-0000-0000-0000-000000000001') returning id as fine \gset
insert into public.deposit_deductions(fine_id,amount,recorded_by,recorded_at) values(:fine,25000,'88888888-0000-0000-0000-000000000001',:'starts');
set local role authenticated;
select set_config('request.jwt.claim.sub','88888888-0000-0000-0000-000000000001',true);
select public.read_report('cash',:'day',:'day','all',0,25)::text as cash \gset
select pg_temp.assert_true((:'cash'::jsonb->>'total')::int=6,'WIB start included and next-day midnight excluded');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'rental_receipts')::numeric=115000,'Operating receipts include DP, rent and extra fine payment');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'deposit_received')::numeric=100000,'Deposit incoming separated');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'deposit_refund')::numeric=75000,'Deposit refund separated');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'rent_refund')::numeric=5000,'Rent refund is outgoing cash');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'cash_net')::numeric=135000,'Cash net does not double count deposit deductions');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'deposit_applied')::numeric=25000,'Noncash deduction reported separately');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'opening_deposit')::numeric=50000,'Opening deposit includes history before period');
select pg_temp.assert_true((:'cash'::jsonb->'summary'->>'closing_deposit')::numeric=50000,'Closing deposit reconciles receipts refunds and deductions');
select pg_temp.assert_true(jsonb_array_length(public.read_report('cash',:'day',:'day','all',2,2)->'rows')=2,'Report pagination returns requested page');
select pg_temp.assert_true((public.read_report('rentals',:'day',:'day','cancelled',0,25)->>'total')::int=1,'Rental report retains cancelled booking history');
select pg_temp.assert_true((public.read_report('rentals',:'day',:'day','active',0,25)->>'total')::int=0,'Rental status filter is applied');
select pg_temp.expect_error(format('select public.read_report(''cash'',%L,%L,''all'',0,25)',(:'day'::date+1)::text,:'day'),'P0001','Invalid period rejected');
select pg_temp.expect_error(format('select public.read_report(''cash'',%L,%L,''all'',0,null)',:'day',:'day'),'P0001','Null page size cannot create unbounded report');
select set_config('request.jwt.claim.sub','88888888-0000-0000-0000-000000000002',true);
select pg_temp.expect_error(format('select public.read_report(''cash'',%L,%L,''all'',0,25)',:'day',:'day'),'42501','QC cannot read cash reports');
reset role;
select pg_temp.assert_true(not has_function_privilege('anon','public.read_report(text,date,date,text,integer,integer)','execute'),'Anonymous reports denied');
rollback;
