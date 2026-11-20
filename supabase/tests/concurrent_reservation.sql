\set ON_ERROR_STOP on
begin;
set local role authenticated;
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000010',true);
select (public.create_reservation(:'request_id',
    (select id from public.customers where name='Race Customer'),
    date_trunc('day',now())+interval '2 days',date_trunc('day',now())+interval '3 days',
    jsonb_build_array(jsonb_build_object('type_id',(select id from public.equipment_types where name='Race Tent'),'quantity',1)),'cash')).id;
\if :hold_lock
select pg_sleep(3);
\endif
commit;
