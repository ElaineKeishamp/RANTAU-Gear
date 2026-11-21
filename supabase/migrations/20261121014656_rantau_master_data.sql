create table rantau_private.master_requests (
    request_id uuid primary key,
    actor_id uuid not null references public.staff_profiles(id),
    entity text not null,
    payload jsonb not null,
    record_id bigint not null
);
alter table rantau_private.master_requests enable row level security;
revoke all on rantau_private.master_requests from public,anon,authenticated;
create index master_requests_actor_idx on rantau_private.master_requests(actor_id);

create function rantau_private.bump_master_version() returns trigger
language plpgsql set search_path='' as $$
begin new.row_version:=old.row_version+1; return new; end;
$$;
do $$ declare t text; begin
    foreach t in array array['categories','equipment_types','equipment_units','packages','customers'] loop
        execute format('alter table public.%I add column row_version bigint not null default 1',t);
        execute format('create trigger master_version before update on public.%I for each row execute function rantau_private.bump_master_version()',t);
        execute format('revoke insert,update,delete on public.%I from anon,authenticated',t);
    end loop;
end $$;
alter table public.package_components add column is_active boolean not null default true;
revoke insert,update,delete on public.package_components from anon,authenticated;
create unique index categories_name_folded_idx on public.categories(lower(btrim(name)));
create unique index types_name_folded_idx on public.equipment_types(category_id,lower(btrim(name)));
create unique index units_code_folded_idx on public.equipment_units(upper(btrim(code)));
create unique index packages_name_folded_idx on public.packages(lower(btrim(name)));

create function rantau_private.ensure_type_capacity(p_type_id bigint) returns void
language plpgsql security definer set search_path='' as $$
declare capacity integer; peak bigint;
begin
    select count(*) into capacity from public.equipment_units u join public.equipment_types t on t.id=u.equipment_type_id
        where t.id=p_type_id and t.is_active and u.state in ('ready','rented');
    with occupied as (
        select case when r.state='returned' then r.returned_at else r.pickup_at end as starts,
            case when r.state='active' and r.due_at<statement_timestamp() then 'infinity'::timestamptz
                when r.state='returned' then r.returned_at+make_interval(mins=>r.buffer_minutes)
                else r.due_at+make_interval(mins=>r.buffer_minutes) end as ends,
            case when r.state='returned' then (select count(*) from public.unit_assignments a
                join public.inspections i on i.assignment_id=a.id join public.equipment_units u on u.id=a.unit_id
                where a.rental_id=r.id and u.equipment_type_id=p_type_id and i.condition='good') else l.quantity end as quantity
        from public.rental_lines l join public.rentals r on r.id=l.rental_id where l.equipment_type_id=p_type_id
            and (r.state in ('active','returned') or (r.state='reserved' and r.pickup_deadline_at>=statement_timestamp()))
    ), relevant as (select * from occupied where ends>statement_timestamp()), points as (
        select statement_timestamp() as at_time union select starts from relevant where starts>statement_timestamp()
    ) select coalesce(max((select coalesce(sum(o.quantity),0) from relevant o where o.starts<=p.at_time and o.ends>p.at_time)),0)
        into peak from points p;
    if peak>capacity then raise exception 'RG_MASTER_CAPACITY'; end if;
end $$;

create function rantau_private.save_master_data(p_request_id uuid,p_entity text,p_id bigint,p_version bigint,p_values jsonb)
returns bigint language plpgsql security definer set search_path='' as $$
declare actor uuid; previous rantau_private.master_requests; body jsonb; table_name text; current_version bigint;
    result_id bigint; parent_id bigint; old_type bigint; old_code text; old_state public.equipment_state;
    desired_state public.equipment_state; active boolean; item record;
begin
    if p_entity='customers' then actor:=rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    else actor:=rantau_private.require_staff(array['super_admin']::public.staff_role[]); end if;
    if p_request_id is null or p_entity not in ('categories','equipment_types','equipment_units','packages','customers')
        or p_entity is null or p_values is null or jsonb_typeof(p_values)<>'object' then raise exception 'RG_MASTER_INPUT'; end if;
    body:=jsonb_build_object('id',p_id,'version',p_version,'values',p_values);
    perform pg_advisory_xact_lock(hashtextextended('rantau.master',0));
    select * into previous from rantau_private.master_requests where request_id=p_request_id;
    if found then
        if previous.actor_id<>actor or previous.entity<>p_entity or previous.payload<>body then raise exception 'RG_MASTER_REQUEST'; end if;
        return previous.record_id;
    end if;
    table_name:=p_entity;
    if p_id is not null then
        if p_entity in ('equipment_units','packages') then
            execute format('select row_version from public.%I where id=$1',table_name) into current_version using p_id;
        else
            execute format('select row_version from public.%I where id=$1 for update',table_name) into current_version using p_id;
        end if;
        if current_version is null then raise exception 'RG_MASTER_MISSING'; end if;
        if p_version is null or current_version<>p_version then raise exception 'RG_MASTER_VERSION'; end if;
    end if;
    active:=coalesce((p_values->>'is_active')::boolean,true);
    if p_entity='equipment_types' and (
        (p_values->>'daily_rate')::numeric<=0 or (p_values->>'daily_rate')::numeric<>trunc((p_values->>'daily_rate')::numeric)
        or (p_values->>'replacement_value')::numeric<0 or (p_values->>'replacement_value')::numeric<>trunc((p_values->>'replacement_value')::numeric)) then raise exception 'RG_MASTER_INPUT'; end if;
    if p_entity='packages' and ((p_values->>'daily_price')::numeric<=0 or (p_values->>'daily_price')::numeric<>trunc((p_values->>'daily_price')::numeric)) then raise exception 'RG_MASTER_INPUT'; end if;
    if p_entity<>'equipment_units' and length(btrim(coalesce(p_values->>'name','')))=0 then raise exception 'RG_MASTER_INPUT'; end if;
    case p_entity
    when 'categories' then
        if not active and exists(select 1 from public.equipment_types where category_id=p_id and is_active) then raise exception 'RG_MASTER_CHILDREN'; end if;
        if p_id is null then
            insert into public.categories(name,is_active) values(btrim(p_values->>'name'),active) returning id into result_id;
        else
            update public.categories set name=btrim(p_values->>'name'),is_active=active where id=p_id returning id into result_id;
        end if;
    when 'equipment_types' then
        parent_id:=(p_values->>'category_id')::bigint;
        if active and not exists(select 1 from public.categories where id=parent_id and is_active) then raise exception 'RG_MASTER_PARENT'; end if;
        if not active and exists(select 1 from public.package_components c join public.packages p on p.id=c.package_id
            where c.equipment_type_id=p_id and c.is_active and p.is_active) then raise exception 'RG_MASTER_PACKAGE'; end if;
        if p_id is null then
            insert into public.equipment_types(category_id,name,daily_rate,replacement_value,description,is_active)
                values(parent_id,btrim(p_values->>'name'),(p_values->>'daily_rate')::numeric,(p_values->>'replacement_value')::numeric,coalesce(p_values->>'description',''),active) returning id into result_id;
        else
            update public.equipment_types set category_id=parent_id,name=btrim(p_values->>'name'),daily_rate=(p_values->>'daily_rate')::numeric,
                replacement_value=(p_values->>'replacement_value')::numeric,description=coalesce(p_values->>'description',''),is_active=active where id=p_id returning id into result_id;
        end if;
        perform rantau_private.ensure_type_capacity(result_id);
    when 'equipment_units' then
        parent_id:=(p_values->>'equipment_type_id')::bigint;
        desired_state:=(p_values->>'state')::public.equipment_state;
        if parent_id is null or desired_state is null or length(btrim(coalesce(p_values->>'code','')))=0 then raise exception 'RG_MASTER_INPUT'; end if;
        if not exists(select 1 from public.equipment_types where id=parent_id and is_active) then raise exception 'RG_MASTER_PARENT'; end if;
        select equipment_type_id,code,state into old_type,old_code,old_state from public.equipment_units where id=p_id;
        perform 1 from public.equipment_types where id in (parent_id,old_type) order by id for update;
        if p_id is not null then
            select row_version into current_version from public.equipment_units where id=p_id for update;
            if current_version<>p_version then raise exception 'RG_MASTER_VERSION'; end if;
        end if;
        if p_id is null and desired_state not in ('ready','maintenance','retired') then raise exception 'RG_MASTER_STATE'; end if;
        if p_id is not null then
            if (old_type<>parent_id or old_code<>upper(btrim(p_values->>'code'))) and exists(select 1 from public.unit_assignments where unit_id=p_id) then raise exception 'RG_MASTER_HISTORY'; end if;
            if (old_state='rented' and desired_state<>old_state) or (old_state='lost' and desired_state not in ('lost','retired'))
                or (desired_state in ('rented','lost') and desired_state<>old_state) then raise exception 'RG_MASTER_STATE'; end if;
        end if;
        if desired_state='ready' and old_state is distinct from 'ready' and not coalesce((p_values->>'qc_confirmed')::boolean,false) then raise exception 'RG_MASTER_QC'; end if;
        if p_id is null then
            insert into public.equipment_units(equipment_type_id,code,state,notes) values(parent_id,upper(btrim(p_values->>'code')),desired_state,coalesce(p_values->>'notes','')) returning id into result_id;
        else
            update public.equipment_units set equipment_type_id=parent_id,code=upper(btrim(p_values->>'code')),state=desired_state,notes=coalesce(p_values->>'notes','') where id=p_id returning id into result_id;
        end if;
        if old_type is not null then perform rantau_private.ensure_type_capacity(old_type); end if;
        perform rantau_private.ensure_type_capacity(parent_id);
    when 'packages' then
        if jsonb_typeof(p_values->'components') is distinct from 'array' then raise exception 'RG_MASTER_COMPONENTS'; end if;
        if active and jsonb_array_length(p_values->'components')=0 then raise exception 'RG_MASTER_COMPONENTS'; end if;
        if exists(select 1 from jsonb_to_recordset(p_values->'components') as x(type_id bigint,quantity integer)
            where type_id is null or quantity is null or quantity<=0)
            or (select count(*)<>count(distinct type_id) from jsonb_to_recordset(p_values->'components') as x(type_id bigint,quantity integer)) then raise exception 'RG_MASTER_COMPONENTS'; end if;
        perform 1 from public.equipment_types where id in
            (select type_id from jsonb_to_recordset(p_values->'components') as x(type_id bigint,quantity integer)
             union select equipment_type_id from public.package_components where package_id=p_id) order by id for update;
        if p_id is not null then
            select row_version into current_version from public.packages where id=p_id for update;
            if current_version<>p_version then raise exception 'RG_MASTER_VERSION'; end if;
        end if;
        if exists(select 1 from jsonb_to_recordset(p_values->'components') as x(type_id bigint,quantity integer)
            left join public.equipment_types t on t.id=x.type_id where t.id is null or (active and not t.is_active)) then raise exception 'RG_MASTER_PARENT'; end if;
        if p_id is null then
            insert into public.packages(name,daily_price,is_active) values(btrim(p_values->>'name'),(p_values->>'daily_price')::numeric,active) returning id into result_id;
        else
            update public.packages set name=btrim(p_values->>'name'),daily_price=(p_values->>'daily_price')::numeric,is_active=active where id=p_id returning id into result_id;
        end if;
        update public.package_components set is_active=false where package_id=result_id
            and equipment_type_id not in (select type_id from jsonb_to_recordset(p_values->'components') as x(type_id bigint,quantity integer)) and is_active;
        for item in select * from jsonb_to_recordset(p_values->'components') as x(type_id bigint,quantity integer) loop
            insert into public.package_components(package_id,equipment_type_id,quantity,is_active) values(result_id,item.type_id,item.quantity,true)
                on conflict(package_id,equipment_type_id) do update set quantity=excluded.quantity,is_active=true;
        end loop;
    when 'customers' then
        if length(btrim(coalesce(p_values->>'phone','')))=0 then raise exception 'RG_MASTER_INPUT'; end if;
        if p_id is null then
            insert into public.customers(name,phone,address,is_active) values(btrim(p_values->>'name'),btrim(p_values->>'phone'),coalesce(p_values->>'address',''),active) returning id into result_id;
        else
            update public.customers set name=btrim(p_values->>'name'),phone=btrim(p_values->>'phone'),address=coalesce(p_values->>'address',''),is_active=active where id=p_id returning id into result_id;
        end if;
    end case;
    insert into rantau_private.master_requests(request_id,actor_id,entity,payload,record_id) values(p_request_id,actor,p_entity,body,result_id);
    return result_id;
end $$;

create function public.save_master_data(p_request_id uuid,p_entity text,p_id bigint,p_version bigint,p_values jsonb)
returns bigint language sql security invoker set search_path='' as $$
    select rantau_private.save_master_data(p_request_id,p_entity,p_id,p_version,p_values);
$$;
revoke all on function rantau_private.bump_master_version(),rantau_private.ensure_type_capacity(bigint),
    rantau_private.save_master_data(uuid,text,bigint,bigint,jsonb), public.save_master_data(uuid,text,bigint,bigint,jsonb) from public,anon,authenticated;
grant execute on function rantau_private.save_master_data(uuid,text,bigint,bigint,jsonb),public.save_master_data(uuid,text,bigint,bigint,jsonb) to authenticated;
revoke execute on function public.save_customer(bigint,text,text,text),rantau_private.save_customer(bigint,text,text,text) from authenticated;
create or replace function rantau_private.create_reservation(
    p_request_id uuid, p_customer_id bigint, p_pickup_at timestamptz, p_due_at timestamptz,
    p_lines jsonb, p_method public.payment_method, p_reference text default '', p_package_id bigint default null
) returns public.rentals language plpgsql security definer set search_path = '' as $$
declare
    actor uuid; request_body jsonb; previous rantau_private.requests; result public.rentals;
    settings public.rental_settings; line record; type_row public.equipment_types;
    daily numeric := 0; days integer; subtotal numeric; package_row public.packages;
begin
    actor := rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    if p_request_id is null or p_customer_id is null or p_pickup_at is null or p_due_at is null or p_method is null then
        raise exception 'Identitas permintaan, pelanggan, jadwal, dan metode pembayaran wajib diisi.' using errcode = '22023';
    end if;
    request_body := jsonb_build_object('customer',p_customer_id,'pickup',p_pickup_at,'due',p_due_at,
        'lines',p_lines,'method',p_method,'reference',coalesce(p_reference,''),'package',p_package_id);
    perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
    select * into previous from rantau_private.requests where request_id=p_request_id;
    if found then
        if previous.actor_id <> actor or previous.operation <> 'reserve' or previous.payload <> request_body then
            raise exception 'ID permintaan sudah digunakan untuk data lain.' using errcode = '22023';
        end if;
        select * into strict result from public.rentals where id=previous.rental_id;
        return result;
    end if;
    if p_due_at <= p_pickup_at or p_due_at <= statement_timestamp() then
        raise exception 'Waktu kembali harus setelah waktu ambil dan belum lewat.' using errcode = '22023';
    end if;
    if p_lines is null or jsonb_typeof(p_lines) <> 'array' then
        raise exception 'Isi reservasi harus berupa daftar alat.' using errcode = '22023';
    end if;
    if jsonb_array_length(p_lines)=0 or exists(select 1 from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)
        where type_id is null or quantity is null or quantity <= 0)
        or (select count(*)<>count(distinct type_id) from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)) then
        raise exception 'Pilih alat tanpa duplikasi dengan jumlah positif.' using errcode = '22023';
    end if;
    if not exists(select 1 from public.customers where id=p_customer_id and is_active) then
        raise exception 'Pelanggan tidak ditemukan atau tidak aktif.';
    end if;
    select * into strict settings from public.rental_settings where id=1;
    if p_pickup_at + make_interval(mins=>settings.pickup_deadline_minutes) < statement_timestamp() then
        raise exception 'Batas pengambilan telah lewat; gunakan jadwal baru.';
    end if;
    -- Every writer of capacity must lock these same type rows in ascending order.
    perform 1 from public.equipment_types t where t.id in
        (select type_id from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)) order by t.id for update;
    for line in select * from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer) order by type_id loop
        select * into type_row from public.equipment_types where id=line.type_id and is_active;
        if not found then raise exception 'Jenis alat tidak ditemukan atau tidak aktif.'; end if;
        if rantau_private.available_quantity(line.type_id,p_pickup_at,p_due_at,settings.buffer_minutes) < line.quantity then
            raise exception 'Stok % tidak cukup untuk periode ini.', type_row.name using errcode = 'P0001';
        end if;
        daily := daily + type_row.daily_rate * line.quantity;
    end loop;
    if p_package_id is not null then
        select * into package_row from public.packages where id=p_package_id and is_active for share;
        if not found then raise exception 'Paket tidak ditemukan atau tidak aktif.'; end if;
        if exists(
            (select equipment_type_id,quantity from public.package_components where package_id=p_package_id and is_active
             except select type_id,quantity from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer))
            union all
            (select type_id,quantity from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)
             except select equipment_type_id,quantity from public.package_components where package_id=p_package_id and is_active)
        ) then raise exception 'Isi reservasi tidak sama dengan isi paket.'; end if;
        daily := package_row.daily_price;
    end if;
    days := greatest(1,ceil(extract(epoch from (p_due_at-p_pickup_at))/86400)::integer);
    subtotal := daily*days;
    insert into public.rentals(customer_id,package_id,pickup_at,due_at,daily_price,rental_days,rent_amount,
        down_payment_due,deposit_due,late_percent_per_day,grace_minutes,buffer_minutes,pickup_deadline_at,created_by)
    values(p_customer_id,p_package_id,p_pickup_at,p_due_at,daily,days,subtotal,
        ceil(subtotal*settings.down_payment_percent/100),ceil(subtotal*settings.deposit_percent/100),
        settings.late_percent_per_day,settings.grace_minutes,settings.buffer_minutes,
        p_pickup_at+make_interval(mins=>settings.pickup_deadline_minutes),actor) returning * into result;
    insert into public.rental_lines(rental_id,equipment_type_id,quantity,name_snapshot,daily_rate_snapshot)
        select result.id,t.id,x.quantity,t.name,t.daily_rate from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)
        join public.equipment_types t on t.id=x.type_id;
    if result.down_payment_due > 0 then
        insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
        values(result.id,'down_payment',result.down_payment_due,p_method,coalesce(p_reference,''),actor);
    end if;
    insert into public.rental_events(rental_id,event_type,actor_id,details)
        values(result.id,'reserved',actor,jsonb_build_object('down_payment',result.down_payment_due));
    insert into rantau_private.requests(request_id,actor_id,operation,payload,rental_id)
        values(p_request_id,actor,'reserve',request_body,result.id);
    return result;
end;
$$;
