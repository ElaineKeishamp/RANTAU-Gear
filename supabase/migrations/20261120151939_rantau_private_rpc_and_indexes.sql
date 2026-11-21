
do $migration$
declare f record; args text; wrapper_sql text;
begin
    for f in
        select p.oid,p.proname,p.proargnames,p.prorettype,p.provolatile,
            pg_get_function_arguments(p.oid) as arguments,
            pg_get_function_identity_arguments(p.oid) as identity_arguments,
            format('%I.%I',rn.nspname,rt.typname) as result_type
        from pg_proc p join pg_namespace n on n.oid=p.pronamespace
        join pg_type rt on rt.oid=p.prorettype join pg_namespace rn on rn.oid=rt.typnamespace
        where n.nspname='public' and p.prosecdef
            and p.proname in ('check_availability','save_customer','create_reservation','cancel_reservation',
                'expire_reservations','calculate_late_fine','pickup_reservation','extend_rental',
                'inspect_return','approve_return_charges','settle_return')
        order by p.proname
    loop
        select coalesce(string_agg(format('%I',a),','),'') into args from unnest(f.proargnames) a;
        execute format('alter function public.%I(%s) set schema rantau_private',f.proname,f.identity_arguments);
        execute format('revoke all on function rantau_private.%I(%s) from public,anon,authenticated',f.proname,f.identity_arguments);
        execute format('grant execute on function rantau_private.%I(%s) to authenticated',f.proname,f.identity_arguments);
        wrapper_sql:=format('create function public.%I(%s) returns %s language sql %s security invoker set search_path = %L as %L',
            f.proname,f.arguments,f.result_type,
            case f.provolatile when 's' then 'stable' when 'i' then 'immutable' else 'volatile' end,
            '',format('select rantau_private.%I(%s)',f.proname,args));
        execute wrapper_sql;
        execute format('revoke all on function public.%I(%s) from public,anon,authenticated',f.proname,f.identity_arguments);
        execute format('grant execute on function public.%I(%s) to authenticated',f.proname,f.identity_arguments);
    end loop;
end;
$migration$;

alter table rantau_private.requests enable row level security;
create index deposit_deductions_fine_idx on public.deposit_deductions(fine_id);
create index deposit_deductions_staff_idx on public.deposit_deductions(recorded_by);
create index fines_approver_idx on public.fines(approved_by);
create index fines_rental_idx on public.fines(rental_id);
create index inspections_staff_idx on public.inspections(inspected_by);
create index package_components_type_idx on public.package_components(equipment_type_id);
create index payments_staff_idx on public.payments(recorded_by);
create index rental_events_actor_idx on public.rental_events(actor_id);
create index rental_events_rental_idx on public.rental_events(rental_id);
create index rentals_creator_idx on public.rentals(created_by);
create index rentals_customer_idx on public.rentals(customer_id);
create index rentals_package_idx on public.rentals(package_id);
create index requests_rental_idx on rantau_private.requests(rental_id);
