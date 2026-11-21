create view public.return_register with (security_invoker=true) as
select r.id,r.rental_number,r.due_at,r.returned_at,r.display_state,
 case when exists(select 1 from public.rental_events e where e.rental_id=r.id and e.event_type='settled') then 'done'
  when exists(select 1 from public.rental_events e where e.rental_id=r.id and e.event_type='charges_approved') then 'payment'
  when r.state='returned' then 'approval' else 'qc' end as return_phase
from public.rental_overview r where r.state in ('active','returned');
revoke all on public.return_register from public,anon,authenticated;
grant select on public.return_register to authenticated;
create function rantau_private.get_return_details(p_rental_id bigint) returns jsonb
language plpgsql security definer set search_path='' as $$
declare actor uuid; role public.staff_role; rental public.rentals; result jsonb; units jsonb; approved boolean; settled boolean;
 deposit numeric; late numeric; proposed numeric; charges numeric;
begin
 actor:=rantau_private.require_staff(array['super_admin','front_office','warehouse_qc']::public.staff_role[]);
 select s.role into strict role from public.staff_profiles s where s.id=actor;
 select * into rental from public.rentals where id=p_rental_id;
 if not found or rental.state not in ('active','returned') then raise exception 'RG_RETURN_STATE'; end if;
 approved:=exists(select 1 from public.rental_events where rental_id=rental.id and event_type='charges_approved');
 settled:=exists(select 1 from public.rental_events where rental_id=rental.id and event_type='settled');
 select coalesce(jsonb_agg(jsonb_build_object('unit_id',u.id,'code',u.code,'name',l.name_snapshot,'replacement_value',t.replacement_value,
  'condition',i.condition,'charge',coalesce(i.proposed_charge,0),'notes',coalesce(i.notes,'')) order by u.code),'[]'::jsonb) into units
 from public.unit_assignments a join public.equipment_units u on u.id=a.unit_id
 join public.equipment_types t on t.id=u.equipment_type_id
 join public.rental_lines l on l.rental_id=a.rental_id and l.equipment_type_id=u.equipment_type_id
 left join public.inspections i on i.assignment_id=a.id where a.rental_id=rental.id;
 result:=jsonb_build_object('id',rental.id,'rental_number',rental.rental_number,'state',rental.state,'due_at',rental.due_at,
 'returned_at',rental.returned_at,'approved',approved,'settled',settled,'units',units);
 -- QC reads inspection records only; customer identity and cash ledgers stay outside this branch.
 if role='warehouse_qc' then return result; end if;
 select coalesce(sum(amount),0) into deposit from public.payments where rental_id=rental.id and kind='deposit_received';
 select coalesce(sum(i.proposed_charge),0) into proposed from public.inspections i join public.unit_assignments a on a.id=i.assignment_id where a.rental_id=rental.id;
 late:=rantau_private.calculate_late_fine(rental.id,coalesce(rental.returned_at,statement_timestamp()));
 if approved then select coalesce(sum(amount),0) into charges from public.fines where rental_id=rental.id;
 else charges:=proposed+late; end if;
 return result||jsonb_build_object('financial',jsonb_build_object('deposit',deposit,'proposed_charge',proposed,'late_charge',late,
  'total_charges',charges,'deposit_applied',least(charges,deposit),'refund',greatest(0,deposit-charges),'additional',greatest(0,charges-deposit)));
end $$;
create function public.get_return_details(p_rental_id bigint) returns jsonb
language sql security invoker set search_path='' as $$ select rantau_private.get_return_details(p_rental_id); $$;
revoke all on function public.get_return_details(bigint),rantau_private.get_return_details(bigint) from public,anon,authenticated;
grant execute on function public.get_return_details(bigint),rantau_private.get_return_details(bigint) to authenticated;
