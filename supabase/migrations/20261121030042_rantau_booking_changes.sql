create function rantau_private.get_booking_change_details(p_rental_id bigint) returns jsonb
language plpgsql security definer set search_path='' as $$
declare rental public.rentals; customer text; dp numeric; deposit numeric;
begin
 perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 select * into rental from public.rentals where id=p_rental_id;
 if not found then raise exception 'RG_CHANGE_STATE'; end if;
 select name into strict customer from public.customers where id=rental.customer_id;
 select coalesce(sum(amount),0) into dp from public.payments where rental_id=rental.id and kind='down_payment';
 select coalesce(sum(amount),0) into deposit from public.payments where rental_id=rental.id and kind='deposit_received';
 return jsonb_build_object('id',rental.id,'rental_number',rental.rental_number,'customer_name',customer,'state',rental.state,
 'pickup_at',rental.pickup_at,'due_at',rental.due_at,'deadline',rental.pickup_deadline_at,'rent_amount',rental.rent_amount,
 'daily_price',rental.daily_price,'dp_received',dp,'deposit_received',deposit,'server_now',statement_timestamp());
end $$;
create function rantau_private.preview_extension(p_rental_id bigint,p_due_at timestamptz) returns jsonb
language plpgsql security definer set search_path='' as $$
declare rental public.rentals; line record; days integer; additional numeric;
begin
 perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 select * into rental from public.rentals where id=p_rental_id;
 if not found or rental.state<>'active' then raise exception 'RG_CHANGE_STATE'; end if;
 if statement_timestamp()>rental.due_at then raise exception 'RG_CHANGE_LATE'; end if;
 if p_due_at is null or p_due_at<=rental.due_at then raise exception 'RG_CHANGE_DUE'; end if;
 for line in select * from public.rental_lines where rental_id=rental.id loop
  if rantau_private.available_quantity(line.equipment_type_id,rental.pickup_at,p_due_at,rental.buffer_minutes,rental.id)<line.quantity then raise exception 'RG_CHANGE_STOCK'; end if;
 end loop;
 days:=greatest(1,ceil(extract(epoch from(p_due_at-rental.pickup_at))/86400)::integer);
 additional:=rental.daily_price*days-rental.rent_amount;
 return jsonb_build_object('old_due',rental.due_at,'new_due',p_due_at,'days',days,'rent_amount',rental.daily_price*days,'additional',additional,'deposit',rental.deposit_due);
end $$;
create function rantau_private.change_booking(p_request_id uuid,p_rental_id bigint,p_action text,p_due_at timestamptz,p_reason text,
 p_expected_due timestamptz,p_expected_additional numeric,p_method public.payment_method,p_reference text)
returns public.rentals language plpgsql security definer set search_path='' as $$
declare actor uuid; result public.rentals; previous rantau_private.requests; body jsonb; line record; days integer; additional numeric;
begin
 actor:=rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 if p_request_id is null or p_rental_id is null or p_action is null or p_action not in ('extend','cancel','expire') then raise exception 'RG_CHANGE_INPUT'; end if;
 body:=jsonb_build_object('rental',p_rental_id,'action',p_action,'due',p_due_at,'reason',coalesce(p_reason,''),
  'expected_due',p_expected_due,'expected_additional',p_expected_additional,'method',p_method,'reference',coalesce(p_reference,''));
 perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
 select * into previous from rantau_private.requests where request_id=p_request_id;
 if found then
  if previous.actor_id<>actor or previous.operation<>'booking_change' or previous.payload<>body then raise exception 'RG_CHANGE_REQUEST'; end if;
  select * into strict result from public.rentals where id=previous.rental_id; return result;
 end if;
 select * into result from public.rentals where id=p_rental_id for update;
 if not found then raise exception 'RG_CHANGE_STATE'; end if;
 case p_action
 when 'extend' then
  if result.state<>'active' then raise exception 'RG_CHANGE_STATE'; end if;
  if statement_timestamp()>result.due_at then raise exception 'RG_CHANGE_LATE'; end if;
  if p_due_at is null or p_due_at<=result.due_at or p_method is null then raise exception 'RG_CHANGE_DUE'; end if;
  days:=greatest(1,ceil(extract(epoch from(p_due_at-result.pickup_at))/86400)::integer);
  additional:=result.daily_price*days-result.rent_amount;
  if p_expected_due is distinct from result.due_at or p_expected_additional is distinct from additional then raise exception 'RG_CHANGE_QUOTE'; end if;
  -- Use the same ordered type locks and existing capacity helper as extend_rental.
  perform 1 from public.equipment_types where id in(select equipment_type_id from public.rental_lines where rental_id=result.id) order by id for update;
  for line in select * from public.rental_lines where rental_id=result.id loop
   if rantau_private.available_quantity(line.equipment_type_id,result.pickup_at,p_due_at,result.buffer_minutes,result.id)<line.quantity then raise exception 'RG_CHANGE_STOCK'; end if;
  end loop;
  if additional>0 then insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
   values(result.id,'rent_payment',additional,p_method,coalesce(p_reference,''),actor); end if;
  insert into public.rental_events(rental_id,event_type,actor_id,details)
   values(result.id,'extended',actor,jsonb_build_object('old_due',result.due_at,'new_due',p_due_at,'additional_rent',additional));
  update public.rentals set due_at=p_due_at,rental_days=days,rent_amount=daily_price*days where id=result.id returning * into result;
 when 'cancel' then
  if result.state<>'reserved' then raise exception 'RG_CHANGE_STATE'; end if;
  if statement_timestamp()>result.pickup_deadline_at then raise exception 'RG_CHANGE_EXPIRED'; end if;
  if length(btrim(coalesce(p_reason,'')))=0 then raise exception 'RG_CHANGE_REASON'; end if;
  result:=rantau_private.cancel_reservation(result.id,p_reason);
 when 'expire' then
  if result.state<>'reserved' then raise exception 'RG_CHANGE_STATE'; end if;
  if statement_timestamp()<=result.pickup_deadline_at then raise exception 'RG_CHANGE_NOT_EXPIRED'; end if;
  update public.rentals set state='expired',cancellation_reason='Tidak diambil dalam 12 jam.' where id=result.id returning * into result;
  insert into public.rental_events(rental_id,event_type,actor_id,details)
   values(result.id,'expired',actor,jsonb_build_object('deadline',result.pickup_deadline_at,'down_payment_forfeited',true));
 end case;
 insert into rantau_private.requests(request_id,actor_id,operation,payload,rental_id) values(p_request_id,actor,'booking_change',body,result.id);
 return result;
end $$;
create function public.get_booking_change_details(p_rental_id bigint) returns jsonb language sql security invoker set search_path='' as $$ select rantau_private.get_booking_change_details(p_rental_id); $$;
create function public.preview_extension(p_rental_id bigint,p_due_at timestamptz) returns jsonb language sql security invoker set search_path='' as $$ select rantau_private.preview_extension(p_rental_id,p_due_at); $$;
create function public.change_booking(p_request_id uuid,p_rental_id bigint,p_action text,p_due_at timestamptz,p_reason text,p_expected_due timestamptz,p_expected_additional numeric,p_method public.payment_method,p_reference text)
returns public.rentals language sql security invoker set search_path='' as $$ select rantau_private.change_booking(p_request_id,p_rental_id,p_action,p_due_at,p_reason,p_expected_due,p_expected_additional,p_method,p_reference); $$;
revoke all on function public.get_booking_change_details(bigint),rantau_private.get_booking_change_details(bigint),public.preview_extension(bigint,timestamptz),rantau_private.preview_extension(bigint,timestamptz),
 public.change_booking(uuid,bigint,text,timestamptz,text,timestamptz,numeric,public.payment_method,text),rantau_private.change_booking(uuid,bigint,text,timestamptz,text,timestamptz,numeric,public.payment_method,text) from public,anon,authenticated;
grant execute on function public.get_booking_change_details(bigint),rantau_private.get_booking_change_details(bigint),public.preview_extension(bigint,timestamptz),rantau_private.preview_extension(bigint,timestamptz),
 public.change_booking(uuid,bigint,text,timestamptz,text,timestamptz,numeric,public.payment_method,text),rantau_private.change_booking(uuid,bigint,text,timestamptz,text,timestamptz,numeric,public.payment_method,text) to authenticated;
