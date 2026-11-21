\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(ok boolean,label text) returns void language plpgsql as $$ begin if ok is distinct from true then raise exception 'FAIL: %',label; end if; raise notice 'PASS: %',label; end $$;
create function pg_temp.expect_error(sql text,expected text,label text) returns void language plpgsql as $$ begin begin execute sql; exception when others then if sqlerrm<>expected and sqlstate<>expected then raise exception 'FAIL: %, got %: %',label,sqlstate,sqlerrm; end if; raise notice 'PASS: %',label; return; end; raise exception 'FAIL: % succeeded',label; end $$;
insert into auth.users(id) values ('66666666-0000-0000-0000-000000000001'),('66666666-0000-0000-0000-000000000002'),('66666666-0000-0000-0000-000000000003');
insert into public.staff_profiles(id,display_name,role) values ('66666666-0000-0000-0000-000000000001','Return Admin','super_admin'),('66666666-0000-0000-0000-000000000002','Return Front','front_office'),('66666666-0000-0000-0000-000000000003','Return QC','warehouse_qc');
create function pg_temp.return_fixture() returns bigint language plpgsql security definer set search_path='' as $$
declare cat bigint; typ bigint; customer bigint; rental bigint; unit bigint;
begin
 insert into public.categories(name) values ('Return category '||gen_random_uuid()) returning id into cat;
 insert into public.equipment_types(category_id,name,daily_rate,replacement_value) values(cat,'Return tent',50000,150000) returning id into typ;
 insert into public.customers(name,phone) values ('Return customer','TEST') returning id into customer;
 insert into public.rentals(customer_id,state,pickup_at,due_at,picked_up_at,daily_price,rental_days,rent_amount,down_payment_due,deposit_due,late_percent_per_day,grace_minutes,buffer_minutes,pickup_deadline_at,created_by)
 values(customer,'active',statement_timestamp()-interval '26 hours',statement_timestamp()-interval '2 hours',statement_timestamp()-interval '26 hours',100000,1,100000,25000,100000,25,60,120,statement_timestamp()-interval '14 hours','66666666-0000-0000-0000-000000000002') returning id into rental;
 insert into public.rental_lines(rental_id,equipment_type_id,quantity,name_snapshot,daily_rate_snapshot) values(rental,typ,2,'Return tent snapshot',50000);
 for idx in 1..2 loop
  insert into public.equipment_units(equipment_type_id,code,state) values(typ,'RETURN-'||rental||'-'||idx,'rented') returning id into unit;
  insert into public.unit_assignments(rental_id,unit_id) values(rental,unit);
 end loop;
 insert into public.payments(rental_id,kind,amount,method,recorded_by) values(rental,'down_payment',25000,'cash','66666666-0000-0000-0000-000000000002'),(rental,'rent_payment',75000,'cash','66666666-0000-0000-0000-000000000002'),(rental,'deposit_received',100000,'cash','66666666-0000-0000-0000-000000000002');
 return rental;
end $$;
select pg_temp.return_fixture() as rental \gset
select min(unit_id) as good,max(unit_id) as lost from public.unit_assignments where rental_id=:rental \gset
select jsonb_build_array(jsonb_build_object('unit_id',:good,'condition','good','charge',0,'notes','Baik'),jsonb_build_object('unit_id',:lost,'condition','lost','charge',150000,'notes','Tidak kembali'))::text as checks \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','66666666-0000-0000-0000-000000000003',true);
select pg_temp.assert_true(not(public.get_return_details(:rental)?'financial'),'QC detail never exposes cash ledgers');
select pg_temp.assert_true(not(public.get_return_details(:rental)?'customer_name'),'QC detail does not expose customer identity');
select pg_temp.assert_true(jsonb_array_length(public.get_return_details(:rental)->'units')=2,'QC receives all assigned units');
select pg_temp.expect_error(format('select public.inspect_return(gen_random_uuid(),%s,%L)',:rental,jsonb_build_array(jsonb_build_object('unit_id',:good,'condition','good','charge',0,'notes',''))::text),'Seluruh unit wajib diperiksa sekaligus; yang hilang harus dicatat sebagai hilang.','Partial return rejected');
select (public.inspect_return('66666666-6666-6666-6666-666666666661',:rental,:'checks')).id;
select pg_temp.assert_true((select state='ready' from public.equipment_units where id=:good),'Good unit released ready for buffer period');
select pg_temp.assert_true((select state='lost' from public.equipment_units where id=:lost),'Lost unit removed from ready stock');
select pg_temp.assert_true((select count(*)=2 from public.inspections i join public.unit_assignments a on a.id=i.assignment_id where a.rental_id=:rental),'All unit inspections recorded once');
select (public.inspect_return('66666666-6666-6666-6666-666666666661',:rental,:'checks')).id;
select pg_temp.assert_true((select count(*)=2 from public.inspections i join public.unit_assignments a on a.id=i.assignment_id where a.rental_id=:rental),'Inspection retry does not duplicate history');
select pg_temp.expect_error(format('select public.approve_return_charges(gen_random_uuid(),%s)',:rental),'42501','QC cannot approve own charges');
select set_config('request.jwt.claim.sub','66666666-0000-0000-0000-000000000002',true);
select pg_temp.assert_true((public.get_return_details(:rental)->'financial'->>'total_charges')::numeric=175000,'Lost compensation plus late fine is additive');
select pg_temp.expect_error(format('select public.settle_return(gen_random_uuid(),%s,''cash'','''')',:rental),'Tagihan harus disetujui Super Admin dahulu.','Front office cannot settle before admin approval');
select pg_temp.expect_error(format('select public.approve_return_charges(gen_random_uuid(),%s)',:rental),'42501','Front office cannot approve charges');
select set_config('request.jwt.claim.sub','66666666-0000-0000-0000-000000000001',true);
select (public.approve_return_charges('66666666-6666-6666-6666-666666666662',:rental)).id;
select (public.approve_return_charges('66666666-6666-6666-6666-666666666662',:rental)).id;
select pg_temp.assert_true((select count(*)=2 and sum(amount)=175000 from public.fines where rental_id=:rental),'Approved loss and lateness fines created once');
select pg_temp.assert_true((public.get_return_details(:rental)->'financial'->>'additional')::numeric=75000,'Settlement quote shows shortage beyond deposit');
select set_config('request.jwt.claim.sub','66666666-0000-0000-0000-000000000002',true);
select (public.settle_return('66666666-6666-6666-6666-666666666663',:rental,'transfer','Final return')).id;
select (public.settle_return('66666666-6666-6666-6666-666666666663',:rental,'transfer','Final return')).id;
select pg_temp.assert_true((select sum(d.amount)=100000 from public.deposit_deductions d join public.fines f on f.id=d.fine_id where f.rental_id=:rental),'Deposit fully deducted once');
select pg_temp.assert_true((select count(*)=1 and sum(amount)=75000 from public.payments where rental_id=:rental and kind='fine_payment'),'Additional payment recorded once');
select pg_temp.assert_true((select count(*)=0 from public.payments where rental_id=:rental and kind='deposit_refund'),'No refund when fines exceed deposit');
select pg_temp.assert_true((select return_phase='done' from public.return_register where id=:rental),'Return register reaches complete phase');
reset role;
select pg_temp.return_fixture() as clean \gset
update public.rentals set due_at=statement_timestamp()+interval '1 hour' where id=:clean;
select jsonb_agg(jsonb_build_object('unit_id',unit_id,'condition','good','charge',0,'notes','Baik'))::text as clean_checks from public.unit_assignments where rental_id=:clean \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','66666666-0000-0000-0000-000000000001',true);
select (public.inspect_return(gen_random_uuid(),:clean,:'clean_checks')).id;
select (public.approve_return_charges(gen_random_uuid(),:clean)).id;
select pg_temp.assert_true((public.get_return_details(:clean)->'financial'->>'refund')::numeric=100000,'All good on-time return refunds full deposit');
select (public.settle_return(gen_random_uuid(),:clean,'cash','Full refund')).id;
select pg_temp.assert_true((select sum(amount)=100000 from public.payments where rental_id=:clean and kind='deposit_refund'),'Full deposit refund ledger recorded');
reset role;
select pg_temp.return_fixture() as damaged \gset
select jsonb_agg(jsonb_build_object('unit_id',unit_id,'condition',case when unit_id=(select min(unit_id) from public.unit_assignments where rental_id=:damaged) then 'damaged' else 'good' end,'charge',case when unit_id=(select min(unit_id) from public.unit_assignments where rental_id=:damaged) then 40000 else 0 end,'notes','QC notes'))::text as damage_checks from public.unit_assignments where rental_id=:damaged \gset
set local role authenticated;
select set_config('request.jwt.claim.sub','66666666-0000-0000-0000-000000000001',true);
select (public.inspect_return(gen_random_uuid(),:damaged,:'damage_checks')).id;
select (public.approve_return_charges(gen_random_uuid(),:damaged)).id;
select pg_temp.assert_true((public.get_return_details(:damaged)->'financial'->>'total_charges')::numeric=65000,'Damage charge also keeps late fine');
select pg_temp.assert_true((public.get_return_details(:damaged)->'financial'->>'refund')::numeric=35000,'Partial deposit refund quote correct');
select (public.settle_return(gen_random_uuid(),:damaged,'cash','Partial refund')).id;
select pg_temp.assert_true((select sum(amount)=35000 from public.payments where rental_id=:damaged and kind='deposit_refund'),'Partial refund recorded as outgoing money');
reset role;
select pg_temp.assert_true(not has_function_privilege('anon','public.get_return_details(bigint)','execute'),'Anonymous return details denied');
rollback;
