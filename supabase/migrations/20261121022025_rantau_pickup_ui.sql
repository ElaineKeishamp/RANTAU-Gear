create function rantau_private.get_pickup_details(p_rental_id bigint) returns jsonb
language plpgsql security definer set search_path='' as $$
declare rental public.rentals; rent_paid numeric; deposit_paid numeric; lines jsonb; units jsonb; customer_name text;
begin
 perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 select * into rental from public.rentals where id=p_rental_id;
 if not found then raise exception 'RG_PICKUP_MISSING'; end if;
 if rental.state<>'reserved' then raise exception 'RG_PICKUP_STATE'; end if;
 if statement_timestamp()<rental.pickup_at or statement_timestamp()>rental.pickup_deadline_at or statement_timestamp()>=rental.due_at then raise exception 'RG_PICKUP_WINDOW'; end if;
 select name into strict customer_name from public.customers where id=rental.customer_id;
 select coalesce(sum(amount),0) into rent_paid from public.payments where rental_id=rental.id and kind in ('down_payment','rent_payment');
 select coalesce(sum(amount),0) into deposit_paid from public.payments where rental_id=rental.id and kind='deposit_received';
 if rent_paid>rental.rent_amount or deposit_paid>rental.deposit_due then raise exception 'RG_PICKUP_BALANCE'; end if;
 select coalesce(jsonb_agg(jsonb_build_object('type_id',equipment_type_id,'name',name_snapshot,'quantity',quantity) order by equipment_type_id),'[]'::jsonb)
  into lines from public.rental_lines where rental_id=rental.id;
 select coalesce(jsonb_agg(jsonb_build_object('id',u.id,'type_id',u.equipment_type_id,'code',u.code) order by u.equipment_type_id,u.code),'[]'::jsonb)
  into units from public.equipment_units u where u.state='ready'
  and u.equipment_type_id in (select equipment_type_id from public.rental_lines where rental_id=rental.id)
  and not exists(select 1 from public.unit_assignments a join public.rentals r on r.id=a.rental_id where a.unit_id=u.id
   and (a.released_at is null or a.released_at+make_interval(mins=>r.buffer_minutes)>statement_timestamp()));
 return jsonb_build_object('id',rental.id,'rental_number',rental.rental_number,'customer_name',customer_name,'pickup_at',rental.pickup_at,'due_at',rental.due_at,
 'deadline',rental.pickup_deadline_at,'rent_amount',rental.rent_amount,'rent_paid',rent_paid,'deposit_paid',deposit_paid,
 'rent_balance',rental.rent_amount-rent_paid,'deposit_balance',rental.deposit_due-deposit_paid,'lines',lines,'units',units);
end $$;
create function public.get_pickup_details(p_rental_id bigint) returns jsonb
language sql security invoker set search_path='' as $$ select rantau_private.get_pickup_details(p_rental_id); $$;
revoke all on function public.get_pickup_details(bigint),rantau_private.get_pickup_details(bigint) from public,anon,authenticated;
grant execute on function public.get_pickup_details(bigint),rantau_private.get_pickup_details(bigint) to authenticated;
create or replace view public.reservation_register with (security_invoker=true) as
select r.id,r.rental_number,c.name as customer_name,r.pickup_at,r.due_at,r.display_state,r.rent_amount,r.deposit_due,
 coalesce((select sum(p.amount) from public.payments p where p.rental_id=r.id and p.kind='down_payment'),0) as dp_received,
 coalesce((select sum(p.amount) from public.payments p where p.rental_id=r.id and p.kind in ('down_payment','rent_payment')),0) as rent_paid,
 coalesce((select sum(p.amount) from public.payments p where p.rental_id=r.id and p.kind='deposit_received'),0) as deposit_received
from public.rental_overview r join public.customers c on c.id=r.customer_id;
