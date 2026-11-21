begin;
create view public.rental_overview with (security_invoker=true) as
select r.*,
    case
        when r.state='reserved' and statement_timestamp()>r.pickup_deadline_at then 'expired'
        when r.state='active' and statement_timestamp()>r.due_at then 'late'
        else r.state::text
    end as display_state,
    (r.state='active' and r.returned_at is null and statement_timestamp()>r.due_at) as is_late
from public.rentals r;
revoke all on public.rental_overview from public,anon,authenticated;
grant select on public.rental_overview to authenticated;
commit;
