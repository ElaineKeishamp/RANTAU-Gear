begin;

create schema if not exists rantau_private;
revoke all on schema rantau_private from public, anon, authenticated;
grant usage on schema rantau_private to authenticated;

create type public.staff_role as enum ('super_admin', 'front_office', 'warehouse_qc');
create type public.equipment_state as enum ('ready', 'rented', 'maintenance', 'lost', 'retired');
create type public.booking_state as enum ('reserved', 'active', 'returned', 'cancelled', 'expired');
create type public.payment_kind as enum ('down_payment', 'rent_payment', 'deposit_received', 'fine_payment', 'deposit_refund', 'rent_refund');
create type public.payment_method as enum ('cash', 'transfer');

create table public.staff_profiles (
    id uuid primary key references auth.users(id) on delete restrict,
    display_name text not null check (length(btrim(display_name)) > 0),
    role public.staff_role not null,
    is_active boolean not null default true,
    created_at timestamptz not null default now()
);
create table public.rental_settings (
    id smallint primary key default 1 check (id = 1),
    down_payment_percent numeric(5,2) not null default 25 check (down_payment_percent between 0 and 100),
    deposit_percent numeric(7,2) not null default 100 check (deposit_percent >= 0),
    late_percent_per_day numeric(7,2) not null default 25 check (late_percent_per_day >= 0),
    grace_minutes integer not null default 60 check (grace_minutes >= 0),
    buffer_minutes integer not null default 120 check (buffer_minutes >= 0),
    pickup_deadline_minutes integer not null default 720 check (pickup_deadline_minutes > 0)
);
insert into public.rental_settings(id) values (1);
create table public.categories (
    id bigint generated always as identity primary key,
    name text not null unique check (length(btrim(name)) > 0),
    is_active boolean not null default true
);
create table public.equipment_types (
    id bigint generated always as identity primary key,
    category_id bigint not null references public.categories(id),
    name text not null check (length(btrim(name)) > 0),
    daily_rate numeric(14,0) not null check (daily_rate > 0),
    replacement_value numeric(14,0) not null check (replacement_value >= 0),
    description text not null default '',
    is_active boolean not null default true,
    unique (category_id, name)
);
create table public.equipment_units (
    id bigint generated always as identity primary key,
    equipment_type_id bigint not null references public.equipment_types(id),
    code text not null unique check (length(btrim(code)) > 0),
    state public.equipment_state not null default 'ready',
    notes text not null default ''
);
create index equipment_units_type_state_idx on public.equipment_units(equipment_type_id, state);
create table public.packages (
    id bigint generated always as identity primary key,
    name text not null unique check (length(btrim(name)) > 0),
    daily_price numeric(14,0) not null check (daily_price > 0),
    is_active boolean not null default true
);
create table public.package_components (
    package_id bigint not null references public.packages(id),
    equipment_type_id bigint not null references public.equipment_types(id),
    quantity integer not null check (quantity > 0),
    primary key (package_id, equipment_type_id)
);
create table public.customers (
    id bigint generated always as identity primary key,
    name text not null check (length(btrim(name)) > 0),
    phone text not null check (length(btrim(phone)) > 0),
    address text not null default '',
    is_active boolean not null default true,
    created_at timestamptz not null default now()
);
create index customers_phone_idx on public.customers(phone);
create sequence public.rental_number_seq;
create table public.rentals (
    id bigint generated always as identity primary key,
    rental_number text not null unique default ('RNT-' || lpad(nextval('public.rental_number_seq')::text, 8, '0')),
    customer_id bigint not null references public.customers(id),
    package_id bigint references public.packages(id),
    state public.booking_state not null default 'reserved',
    pickup_at timestamptz not null,
    due_at timestamptz not null,
    picked_up_at timestamptz,
    returned_at timestamptz,
    daily_price numeric(14,0) not null check (daily_price > 0),
    rental_days integer not null check (rental_days > 0),
    rent_amount numeric(14,0) not null check (rent_amount > 0),
    down_payment_due numeric(14,0) not null check (down_payment_due >= 0 and down_payment_due <= rent_amount),
    deposit_due numeric(14,0) not null check (deposit_due >= 0),
    late_percent_per_day numeric(7,2) not null check (late_percent_per_day >= 0),
    grace_minutes integer not null check (grace_minutes >= 0),
    buffer_minutes integer not null check (buffer_minutes >= 0),
    pickup_deadline_at timestamptz not null,
    cancellation_reason text,
    notes text not null default '',
    created_by uuid not null references public.staff_profiles(id),
    created_at timestamptz not null default now(),
    check (due_at > pickup_at),
    check (pickup_deadline_at > pickup_at),
    check (picked_up_at is null or picked_up_at >= pickup_at),
    check (returned_at is null or (picked_up_at is not null and returned_at >= picked_up_at))
);
create index rentals_schedule_idx on public.rentals(state, pickup_at, due_at);
create table public.rental_lines (
    rental_id bigint not null references public.rentals(id),
    equipment_type_id bigint not null references public.equipment_types(id),
    quantity integer not null check (quantity > 0),
    name_snapshot text not null,
    daily_rate_snapshot numeric(14,0) not null check (daily_rate_snapshot > 0),
    primary key (rental_id, equipment_type_id)
);
create index rental_lines_type_idx on public.rental_lines(equipment_type_id, rental_id);
create table public.unit_assignments (
    id bigint generated always as identity primary key,
    rental_id bigint not null references public.rentals(id),
    unit_id bigint not null references public.equipment_units(id),
    assigned_at timestamptz not null default now(),
    released_at timestamptz,
    unique (rental_id, unit_id),
    check (released_at is null or released_at >= assigned_at)
);
create unique index unit_one_open_assignment_idx on public.unit_assignments(unit_id) where released_at is null;
create table public.inspections (
    id bigint generated always as identity primary key,
    assignment_id bigint not null unique references public.unit_assignments(id),
    condition text not null check (condition in ('good', 'damaged', 'lost')),
    notes text not null default '',
    proposed_charge numeric(14,0) not null default 0 check (proposed_charge >= 0),
    inspected_by uuid not null references public.staff_profiles(id),
    inspected_at timestamptz not null default now(),
    check ((condition = 'good' and proposed_charge = 0) or length(btrim(notes)) > 0)
);
create table public.fines (
    id bigint generated always as identity primary key,
    rental_id bigint not null references public.rentals(id),
    inspection_id bigint unique references public.inspections(id),
    kind text not null check (kind in ('late', 'damage', 'loss')),
    amount numeric(14,0) not null check (amount > 0),
    reason text not null check (length(btrim(reason)) > 0),
    approved_by uuid not null references public.staff_profiles(id),
    created_at timestamptz not null default now()
);
create table public.payments (
    id bigint generated always as identity primary key,
    rental_id bigint not null references public.rentals(id),
    kind public.payment_kind not null,
    amount numeric(14,0) not null check (amount > 0),
    method public.payment_method not null,
    reference text not null default '',
    recorded_by uuid not null references public.staff_profiles(id),
    recorded_at timestamptz not null default now()
);
create index payments_rental_idx on public.payments(rental_id);
create table public.deposit_deductions (
    id bigint generated always as identity primary key,
    fine_id bigint not null references public.fines(id),
    amount numeric(14,0) not null check (amount > 0),
    recorded_by uuid not null references public.staff_profiles(id),
    recorded_at timestamptz not null default now()
);
create table public.rental_events (
    id bigint generated always as identity primary key,
    rental_id bigint not null references public.rentals(id),
    event_type text not null,
    actor_id uuid not null references public.staff_profiles(id),
    details jsonb not null default '{}',
    created_at timestamptz not null default now()
);
create table public.audit_log (
    id bigint generated always as identity primary key,
    table_name text not null,
    row_id text not null,
    action text not null,
    actor_id uuid,
    before_data jsonb,
    after_data jsonb,
    created_at timestamptz not null default now()
);
create table rantau_private.requests (
    request_id uuid primary key,
    actor_id uuid not null,
    operation text not null,
    payload jsonb not null,
    rental_id bigint not null references public.rentals(id)
);

create function rantau_private.is_staff(p_roles public.staff_role[]) returns boolean
language sql stable security definer set search_path = '' as $$
    select exists (select 1 from public.staff_profiles p
        where p.id = (select auth.uid()) and p.is_active and p.role = any(p_roles));
$$;
create function rantau_private.require_staff(p_roles public.staff_role[]) returns uuid
language plpgsql stable security definer set search_path = '' as $$
begin
    if not rantau_private.is_staff(p_roles) then
        raise exception 'Akses ditolak atau akun tidak aktif.' using errcode = '42501';
    end if;
    return auth.uid();
end;
$$;
create function rantau_private.capture_audit() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
    insert into public.audit_log(table_name, row_id, action, actor_id, before_data, after_data)
    values (tg_table_name, coalesce(to_jsonb(new)->>'id', to_jsonb(old)->>'id', to_jsonb(new)::text),
        tg_op, auth.uid(), case when tg_op <> 'INSERT' then to_jsonb(old) end, to_jsonb(new));
    return new;
end;
$$;
create function rantau_private.reject_history_change() returns trigger
language plpgsql set search_path = '' as $$
begin
    raise exception 'Riwayat tidak boleh dihapus atau ditimpa; buat catatan koreksi.' using errcode = '23514';
end;
$$;

do $$
declare t text;
begin
    foreach t in array array['staff_profiles','rental_settings','categories','equipment_types','equipment_units',
        'packages','package_components','customers','rentals','rental_lines','unit_assignments','inspections',
        'fines','payments','deposit_deductions','rental_events'] loop
        execute format('revoke all on public.%I from public, anon, authenticated', t);
        execute format('alter table public.%I enable row level security', t);
        execute format('create trigger audit_change after insert or update on public.%I for each row execute function rantau_private.capture_audit()', t);
        execute format('create policy staff_read on public.%I for select to authenticated using (rantau_private.is_staff(array[''super_admin'',''front_office'',''warehouse_qc'']::public.staff_role[]))', t);
        execute format('create trigger protect_delete before delete on public.%I for each row execute function rantau_private.reject_history_change()', t);
    end loop;
    foreach t in array array['payments','fines','deposit_deductions','rental_events','audit_log'] loop
        execute format('create trigger protect_update before update on public.%I for each row execute function rantau_private.reject_history_change()', t);
    end loop;
    foreach t in array array['rental_settings','categories','equipment_types','packages','package_components'] loop
        execute format('create policy admin_insert on public.%I for insert to authenticated with check (rantau_private.is_staff(array[''super_admin'']::public.staff_role[]))', t);
        execute format('create policy admin_update on public.%I for update to authenticated using (rantau_private.is_staff(array[''super_admin'']::public.staff_role[])) with check (rantau_private.is_staff(array[''super_admin'']::public.staff_role[]))', t);
        execute format('grant select, insert, update on public.%I to authenticated', t);
    end loop;
end;
$$;
revoke all on public.audit_log from public,anon,authenticated;
alter table public.audit_log enable row level security;
create policy admin_audit_read on public.audit_log for select to authenticated
    using (rantau_private.is_staff(array['super_admin']::public.staff_role[]));
-- RLS hides customer contact details from warehouse/QC staff.
drop policy staff_read on public.customers;
create policy office_customer_read on public.customers for select to authenticated
    using (rantau_private.is_staff(array['super_admin','front_office']::public.staff_role[]));
drop policy staff_read on public.payments;
create policy office_payment_read on public.payments for select to authenticated
    using (rantau_private.is_staff(array['super_admin','front_office']::public.staff_role[]));
drop policy staff_read on public.staff_profiles;
create policy own_profile_read on public.staff_profiles for select to authenticated
    using (id = (select auth.uid()) or rantau_private.is_staff(array['super_admin']::public.staff_role[]));

grant usage on schema public to authenticated;
grant select on public.staff_profiles, public.rental_settings, public.categories, public.equipment_types,
    public.equipment_units, public.packages, public.package_components, public.customers, public.rentals,
    public.rental_lines, public.unit_assignments, public.inspections, public.fines, public.payments,
    public.deposit_deductions, public.rental_events, public.audit_log to authenticated;
do $$
declare s text;
begin
    foreach s in array array['categories_id_seq','equipment_types_id_seq','equipment_units_id_seq','packages_id_seq',
        'customers_id_seq','rentals_id_seq','unit_assignments_id_seq','inspections_id_seq','fines_id_seq',
        'payments_id_seq','deposit_deductions_id_seq','rental_events_id_seq','audit_log_id_seq','rental_number_seq'] loop
        execute format('revoke all on sequence public.%I from public,anon,authenticated',s);
    end loop;
end;
$$;
grant usage on sequence public.categories_id_seq, public.equipment_types_id_seq, public.packages_id_seq to authenticated;
revoke all on all tables in schema rantau_private from public, anon, authenticated;
revoke all on all functions in schema rantau_private from public, anon, authenticated;
grant execute on function rantau_private.is_staff(public.staff_role[]) to authenticated;

commit;
