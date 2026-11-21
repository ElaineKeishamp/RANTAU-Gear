-- Preview is an estimate, not a capacity hold. The existing reservation RPC rechecks capacity atomically.
create function rantau_private.preview_reservation(p_pickup_at timestamptz,p_due_at timestamptz,p_lines jsonb,p_package_id bigint default null)
returns jsonb language plpgsql security definer set search_path='' as $$
declare settings public.rental_settings; item record; rate numeric:=0; days integer; price numeric; package public.packages;
begin
 perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 if p_pickup_at is null or p_due_at is null or p_due_at<=p_pickup_at or p_due_at<=statement_timestamp()
  or jsonb_typeof(p_lines) is distinct from 'array' then raise exception 'RG_BOOKING_INPUT'; end if;
 select * into strict settings from public.rental_settings where id=1;
 if p_pickup_at+make_interval(mins=>settings.pickup_deadline_minutes)<statement_timestamp() then raise exception 'RG_BOOKING_INPUT'; end if;
 if jsonb_array_length(p_lines)=0 or exists(select 1 from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer) where type_id is null or quantity is null or quantity<=0)
  or (select count(*)<>count(distinct type_id) from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)) then raise exception 'RG_BOOKING_INPUT'; end if;
 for item in select * from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer) loop
  select daily_rate into price from public.equipment_types where id=item.type_id and is_active;
  if not found then raise exception 'RG_BOOKING_CHANGED'; end if;
  if rantau_private.available_quantity(item.type_id,p_pickup_at,p_due_at,settings.buffer_minutes)<item.quantity then raise exception 'RG_BOOKING_STOCK'; end if;
  rate:=rate+price*item.quantity;
 end loop;
 if p_package_id is not null then
  select * into package from public.packages where id=p_package_id and is_active;
  if not found then raise exception 'RG_BOOKING_CHANGED'; end if;
  if exists((select equipment_type_id,quantity from public.package_components where package_id=p_package_id and is_active
    except select type_id,quantity from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer))
    union all (select type_id,quantity from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)
    except select equipment_type_id,quantity from public.package_components where package_id=p_package_id and is_active)) then raise exception 'RG_BOOKING_CHANGED'; end if;
  rate:=package.daily_price;
 end if;
 days:=greatest(1,ceil(extract(epoch from(p_due_at-p_pickup_at))/86400)::integer);
 return jsonb_build_object('daily_price',rate,'rental_days',days,'rent_amount',rate*days,
 'down_payment_due',ceil(rate*days*settings.down_payment_percent/100),'deposit_due',ceil(rate*days*settings.deposit_percent/100),
 'pickup_deadline_at',p_pickup_at+make_interval(mins=>settings.pickup_deadline_minutes));
end $$;

create function rantau_private.reserve_with_dp(p_request_id uuid,p_customer_id bigint,p_pickup_at timestamptz,p_due_at timestamptz,
 p_lines jsonb,p_method public.payment_method,p_reference text,p_package_id bigint,p_expected_rent numeric,p_expected_dp numeric,p_expected_deposit numeric)
returns public.rentals language plpgsql security definer set search_path='' as $$
declare result public.rentals;
begin
 perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 if p_expected_rent is null or p_expected_dp is null or p_expected_deposit is null then raise exception 'RG_BOOKING_INPUT'; end if;
 result:=rantau_private.create_reservation(p_request_id,p_customer_id,p_pickup_at,p_due_at,p_lines,p_method,p_reference,p_package_id);
 if result.rent_amount<>p_expected_rent or result.down_payment_due<>p_expected_dp or result.deposit_due<>p_expected_deposit then
  raise exception 'RG_BOOKING_CHANGED';
 end if;
 return result;
exception when raise_exception then
 if sqlerrm like 'Stok % tidak cukup untuk periode ini.' then raise exception 'RG_BOOKING_STOCK'; end if;
 raise;
end $$;
create function public.preview_reservation(p_pickup_at timestamptz,p_due_at timestamptz,p_lines jsonb,p_package_id bigint default null)
returns jsonb language sql security invoker set search_path='' as $$ select rantau_private.preview_reservation(p_pickup_at,p_due_at,p_lines,p_package_id); $$;
create function public.reserve_with_dp(p_request_id uuid,p_customer_id bigint,p_pickup_at timestamptz,p_due_at timestamptz,
 p_lines jsonb,p_method public.payment_method,p_reference text,p_package_id bigint,p_expected_rent numeric,p_expected_dp numeric,p_expected_deposit numeric)
returns public.rentals language sql security invoker set search_path='' as $$
 select rantau_private.reserve_with_dp(p_request_id,p_customer_id,p_pickup_at,p_due_at,p_lines,p_method,p_reference,p_package_id,p_expected_rent,p_expected_dp,p_expected_deposit);
$$;
revoke all on function rantau_private.preview_reservation(timestamptz,timestamptz,jsonb,bigint),public.preview_reservation(timestamptz,timestamptz,jsonb,bigint),
 rantau_private.reserve_with_dp(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint,numeric,numeric,numeric),public.reserve_with_dp(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint,numeric,numeric,numeric) from public,anon,authenticated;
grant execute on function rantau_private.preview_reservation(timestamptz,timestamptz,jsonb,bigint),public.preview_reservation(timestamptz,timestamptz,jsonb,bigint),
 rantau_private.reserve_with_dp(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint,numeric,numeric,numeric),public.reserve_with_dp(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint,numeric,numeric,numeric) to authenticated;
create view public.reservation_register with (security_invoker=true) as
select r.id,r.rental_number,c.name as customer_name,r.pickup_at,r.due_at,r.display_state,r.rent_amount,r.deposit_due,
 coalesce((select sum(p.amount) from public.payments p where p.rental_id=r.id and p.kind='down_payment'),0) as dp_received
from public.rental_overview r join public.customers c on c.id=r.customer_id;
revoke all on public.reservation_register from public,anon,authenticated;
grant select on public.reservation_register to authenticated;
