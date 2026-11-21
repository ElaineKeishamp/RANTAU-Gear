\set ON_ERROR_STOP on
begin;
create function pg_temp.assert_true(ok boolean,label text) returns void language plpgsql as $$ begin if ok is distinct from true then raise exception 'FAIL: %',label; end if; raise notice 'PASS: %',label; end $$;
create function pg_temp.expect_error(sql text,expected text,label text) returns void language plpgsql as $$
begin begin execute sql; exception when others then if sqlstate<>expected then raise exception 'FAIL: %, got %: %',label,sqlstate,sqlerrm; end if; raise notice 'PASS: %',label; return; end; raise exception 'FAIL: % succeeded',label; end $$;
insert into auth.users(id) values ('11111111-0000-0000-0000-000000000001'),('11111111-0000-0000-0000-000000000002'),('11111111-0000-0000-0000-000000000003');
insert into public.staff_profiles(id,display_name,role) values
('11111111-0000-0000-0000-000000000001','Master Admin','super_admin'),('11111111-0000-0000-0000-000000000002','Master Front','front_office'),('11111111-0000-0000-0000-000000000003','Master QC','warehouse_qc');
set local role authenticated;
select set_config('request.jwt.claim.sub','11111111-0000-0000-0000-000000000001',true);
select public.save_master_data('00000001-0000-0000-0000-000000000001','categories',null,0,'{"name":"Master Tenda","is_active":true}') as cat \gset
select pg_temp.assert_true((select row_version=1 from public.categories where id=:cat),'New master starts at version one');
select pg_temp.assert_true(public.save_master_data('00000001-0000-0000-0000-000000000001','categories',null,0,'{"name":"Master Tenda","is_active":true}')=:cat,'Create retry returns same master ID');
select pg_temp.assert_true((select count(*)=1 from public.categories where name='Master Tenda'),'Create retry does not duplicate master');
select pg_temp.expect_error('select public.save_master_data(''00000001-0000-0000-0000-000000000002'',''categories'',null,0,''{"name":" master tenda ","is_active":true}'')','23505','Case-insensitive duplicate category rejected');
select public.save_master_data('00000001-0000-0000-0000-000000000003','categories',:cat,1,'{"name":"Master Outdoor","is_active":true}');
select pg_temp.expect_error(format('select public.save_master_data(%L,%L,%s,1,%L::jsonb)','00000001-0000-0000-0000-000000000004','categories',:cat,'{"name":"Stale overwrite","is_active":true}'),'P0001','Stale version cannot overwrite another edit');
select pg_temp.assert_true((select name='Master Outdoor' and row_version=2 from public.categories where id=:cat),'Stale edit preserves committed name and version');
select public.save_master_data('00000002-0000-0000-0000-000000000001','equipment_types',null,0,jsonb_build_object('name','Master Dome','category_id',:cat,'daily_rate',100000,'replacement_value',1000000,'is_active',true)) as typ \gset
select pg_temp.expect_error(format('select public.save_master_data(%L,%L,%s,2,%L::jsonb)','00000002-0000-0000-0000-000000000002','categories',:cat,'{"name":"Master Outdoor","is_active":false}'),'P0001','Category with active types cannot be disabled');
select public.save_master_data('00000003-0000-0000-0000-000000000001','equipment_units',null,0,jsonb_build_object('equipment_type_id',:typ,'code','master-001','state','ready','qc_confirmed',true)) as unit \gset
select pg_temp.assert_true((select code='MASTER-001' from public.equipment_units where id=:unit),'Unit code normalized without changing identity');
select pg_temp.expect_error(format('select public.save_master_data(%L,%L,null,0,%L::jsonb)','00000003-0000-0000-0000-000000000002','equipment_units',jsonb_build_object('equipment_type_id',:typ,'code','NO-QC','state','ready','qc_confirmed',false)::text),'P0001','Ready unit requires inspection confirmation');
select public.save_master_data('00000004-0000-0000-0000-000000000001','customers',null,0,'{"name":"Master Customer","phone":"080000000","address":"Alamat uji","is_active":true}') as customer \gset
select now()+interval '1 day' as pickup,now()+interval '2 days' as due \gset
select (public.create_reservation('00000005-0000-0000-0000-000000000001',:customer,:'pickup',:'due',jsonb_build_array(jsonb_build_object('type_id',:typ,'quantity',1)),'cash')).id as rent \gset
select pg_temp.expect_error(format('select public.save_master_data(%L,%L,%s,1,%L::jsonb)','00000003-0000-0000-0000-000000000003','equipment_units',:unit,jsonb_build_object('equipment_type_id',:typ,'code','MASTER-001','state','retired')::text),'P0001','Cannot retire capacity promised to a future reservation');
select pg_temp.assert_true((select state='ready' and row_version=1 from public.equipment_units where id=:unit),'Rejected stock reduction is fully rolled back');
select public.save_master_data('00000002-0000-0000-0000-000000000003','equipment_types',:typ,1,jsonb_build_object('name','Master Dome','category_id',:cat,'daily_rate',120000,'replacement_value',1000000,'is_active',true));
select pg_temp.assert_true((select daily_price=100000 and rent_amount=100000 from public.rentals where id=:rent),'Master price edit does not change agreed rental snapshot');
select public.save_master_data('00000003-0000-0000-0000-000000000004','equipment_units',null,0,jsonb_build_object('equipment_type_id',:typ,'code','MASTER-002','state','ready','qc_confirmed',true)) as spare \gset
select public.save_master_data('00000003-0000-0000-0000-000000000005','equipment_units',:spare,1,jsonb_build_object('equipment_type_id',:typ,'code','MASTER-002','state','retired'));
select pg_temp.assert_true((select state='retired' from public.equipment_units where id=:spare),'Unused spare can be deactivated without deleting history');
select public.save_master_data('00000002-0000-0000-0000-000000000004','equipment_types',null,0,jsonb_build_object('name','Master Sleeping Bag','category_id',:cat,'daily_rate',30000,'replacement_value',300000,'is_active',true)) as typ2 \gset
select public.save_master_data('00000003-0000-0000-0000-000000000006','equipment_units',null,0,jsonb_build_object('equipment_type_id',:typ2,'code','MASTER-SLP','state','ready','qc_confirmed',true));
select public.save_master_data('00000006-0000-0000-0000-000000000001','packages',null,0,jsonb_build_object('name','Master Package','daily_price',80000,'is_active',true,'components',jsonb_build_array(jsonb_build_object('type_id',:typ,'quantity',1),jsonb_build_object('type_id',:typ2,'quantity',1)))) as package \gset
select public.save_master_data('00000006-0000-0000-0000-000000000002','packages',:package,1,jsonb_build_object('name','Master Package','daily_price',25000,'is_active',true,'components',jsonb_build_array(jsonb_build_object('type_id',:typ2,'quantity',1))));
select pg_temp.assert_true((select count(*)=2 from public.package_components where package_id=:package),'Removing package component preserves old row');
select pg_temp.assert_true((select not is_active from public.package_components where package_id=:package and equipment_type_id=:typ),'Removed package component is inactive');
select (public.create_reservation('00000005-0000-0000-0000-000000000002',:customer,:'pickup',:'due',jsonb_build_array(jsonb_build_object('type_id',:typ2,'quantity',1)),'cash','',:package)).id as package_rent \gset
select pg_temp.assert_true((select daily_price=25000 from public.rentals where id=:package_rent),'Reservation uses only current active package components');
select pg_temp.expect_error(format('select public.save_master_data(%L,%L,%s,1,%L::jsonb)','00000002-0000-0000-0000-000000000005','equipment_types',:typ2,jsonb_build_object('name','Master Sleeping Bag','category_id',:cat,'daily_rate',30000,'replacement_value',300000,'is_active',false)::text),'P0001','Cannot deactivate type used by active package');
select pg_temp.expect_error('update public.categories set name=''Bypass''','42501','Direct master updates bypassing versioned RPC are denied');
select pg_temp.expect_error('delete from public.customers','42501','Customer history cannot be deleted by client');
select set_config('request.jwt.claim.sub','11111111-0000-0000-0000-000000000002',true);
select public.save_master_data('00000004-0000-0000-0000-000000000002','customers',null,0,'{"name":"Front Customer","phone":"081234","is_active":true}') as front_customer \gset
select pg_temp.assert_true((select name='Front Customer' from public.customers where id=:front_customer),'Front office can manage customers');
select pg_temp.expect_error('select public.save_master_data(''00000001-0000-0000-0000-000000000005'',''categories'',null,0,''{"name":"Forbidden"}'')','42501','Front office cannot alter inventory master');
select set_config('request.jwt.claim.sub','11111111-0000-0000-0000-000000000003',true);
select pg_temp.expect_error('select public.save_master_data(''00000004-0000-0000-0000-000000000003'',''customers'',null,0,''{"name":"Forbidden","phone":"08"}'')','42501','QC cannot manage customer contacts');
reset role;
select pg_temp.assert_true((select count(*)>0 from public.audit_log where table_name='equipment_units'),'Master changes retain audit evidence');
rollback;
