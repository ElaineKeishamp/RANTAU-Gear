\set ON_ERROR_STOP on
insert into auth.users(id) values ('00000000-0000-0000-0000-000000000010');
insert into public.staff_profiles(id,display_name,role) values ('00000000-0000-0000-0000-000000000010','Race Front','front_office');
insert into public.categories(name) values ('Race Category');
insert into public.equipment_types(category_id,name,daily_rate,replacement_value)
select id,'Race Tent',100000,1000000 from public.categories where name='Race Category';
insert into public.equipment_units(equipment_type_id,code)
select id,'RACE-001' from public.equipment_types where name='Race Tent';
insert into public.customers(name,phone) values ('Race Customer','080000001');
