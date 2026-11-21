begin;

create function rantau_private.available_quantity(
    p_type_id bigint, p_pickup_at timestamptz, p_due_at timestamptz,
    p_buffer_minutes integer, p_ignore_rental bigint default null
) returns integer language sql stable security definer set search_path = '' as $$
    with bounds as (
        select p_pickup_at as starts, p_due_at + make_interval(mins => p_buffer_minutes) as ends
    ), occupied as (
        select r.id, case when r.state = 'returned' then
            (select count(*)::integer from public.unit_assignments a join public.inspections i on i.assignment_id=a.id
             join public.equipment_units u on u.id=a.unit_id where a.rental_id=r.id and u.equipment_type_id=l.equipment_type_id and i.condition='good')
            else l.quantity end as quantity,
            case when r.state = 'returned' then r.returned_at else r.pickup_at end as starts,
            case
                when r.state = 'active' and r.due_at < statement_timestamp() then 'infinity'::timestamptz
                when r.state = 'returned' then r.returned_at + make_interval(mins => r.buffer_minutes)
                else r.due_at + make_interval(mins => r.buffer_minutes)
            end as ends
        from public.rental_lines l join public.rentals r on r.id = l.rental_id
        where l.equipment_type_id = p_type_id and (p_ignore_rental is null or r.id <> p_ignore_rental)
            and (r.state in ('active','returned') or (r.state = 'reserved' and statement_timestamp() <= r.pickup_deadline_at))
    ), relevant as (
        select o.* from occupied o, bounds b where o.starts < b.ends and o.ends > b.starts
    ), points as (
        select starts as at_time from bounds
        union select r.starts from relevant r, bounds b where r.starts >= b.starts and r.starts < b.ends
    ), peak as (
        select coalesce(max((select coalesce(sum(r.quantity),0) from relevant r
            where r.starts <= p.at_time and r.ends > p.at_time)),0) as used from points p
    )
    select greatest(0, (select count(*) from public.equipment_units u
        join public.equipment_types t on t.id = u.equipment_type_id
        where u.equipment_type_id = p_type_id and t.is_active and u.state in ('ready','rented')) - peak.used)::integer
    from peak;
$$;

create function public.check_availability(p_type_id bigint, p_pickup_at timestamptz, p_due_at timestamptz)
returns integer language plpgsql stable security definer set search_path = '' as $$
declare buffer integer;
begin
    perform rantau_private.require_staff(array['super_admin','front_office','warehouse_qc']::public.staff_role[]);
    if p_type_id is null or p_pickup_at is null or p_due_at is null or p_due_at <= p_pickup_at then
        raise exception 'Jenis alat dan rentang waktu wajib valid.' using errcode = '22023';
    end if;
    select buffer_minutes into strict buffer from public.rental_settings where id = 1;
    return rantau_private.available_quantity(p_type_id, p_pickup_at, p_due_at, buffer);
end;
$$;

create function public.save_customer(p_id bigint, p_name text, p_phone text, p_address text default '')
returns public.customers language plpgsql security definer set search_path = '' as $$
declare result public.customers;
begin
    perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    if p_name is null or p_phone is null or length(btrim(p_name)) = 0 or length(btrim(p_phone)) = 0 then
        raise exception 'Nama dan nomor telepon wajib diisi.' using errcode = '22023';
    end if;
    if p_id is null then
        insert into public.customers(name,phone,address) values (btrim(p_name),btrim(p_phone),coalesce(p_address,'')) returning * into result;
    else
        update public.customers set name=btrim(p_name),phone=btrim(p_phone),address=coalesce(p_address,'')
        where id=p_id returning * into result;
        if not found then raise exception 'Pelanggan tidak ditemukan.'; end if;
    end if;
    return result;
end;
$$;

create function public.create_reservation(
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
            (select equipment_type_id,quantity from public.package_components where package_id=p_package_id
             except select type_id,quantity from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer))
            union all
            (select type_id,quantity from jsonb_to_recordset(p_lines) as x(type_id bigint,quantity integer)
             except select equipment_type_id,quantity from public.package_components where package_id=p_package_id)
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

create function public.cancel_reservation(p_rental_id bigint, p_reason text)
returns public.rentals language plpgsql security definer set search_path = '' as $$
declare actor uuid; result public.rentals;
begin
    actor := rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    if p_reason is null or length(btrim(p_reason))=0 then raise exception 'Alasan pembatalan wajib diisi.'; end if;
    select * into result from public.rentals where id=p_rental_id for update;
    if not found then raise exception 'Reservasi tidak ditemukan.'; end if;
    if result.state <> 'reserved' then raise exception 'Hanya reservasi yang belum diambil dapat dibatalkan.'; end if;
    if statement_timestamp() > result.pickup_deadline_at then raise exception 'Reservasi sudah melewati batas ambil.'; end if;
    update public.rentals set state='cancelled',cancellation_reason=btrim(p_reason) where id=p_rental_id returning * into result;
    insert into public.rental_events(rental_id,event_type,actor_id,details)
        values(result.id,'cancelled',actor,jsonb_build_object('reason',p_reason,'down_payment_forfeited',true));
    return result;
end;
$$;

create function public.expire_reservations() returns integer
language plpgsql security definer set search_path = '' as $$
declare actor uuid; expired public.rentals; total integer:=0;
begin
    actor := rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    for expired in update public.rentals set state='expired',cancellation_reason='Tidak diambil dalam 12 jam.'
        where state='reserved' and pickup_deadline_at < statement_timestamp() returning * loop
        insert into public.rental_events(rental_id,event_type,actor_id,details)
            values(expired.id,'expired',actor,jsonb_build_object('deadline',expired.pickup_deadline_at,'down_payment_forfeited',true));
        total:=total+1;
    end loop;
    return total;
end;
$$;

create function public.calculate_late_fine(p_rental_id bigint, p_returned_at timestamptz)
returns numeric language plpgsql stable security definer set search_path = '' as $$
declare rental public.rentals; late_seconds numeric;
begin
    perform rantau_private.require_staff(array['super_admin','front_office','warehouse_qc']::public.staff_role[]);
    select * into rental from public.rentals where id=p_rental_id;
    if not found or p_returned_at is null or p_returned_at < rental.pickup_at then raise exception 'Sewa atau waktu kembali tidak valid.'; end if;
    late_seconds:=extract(epoch from (coalesce(rental.returned_at,p_returned_at)-rental.due_at-make_interval(mins=>rental.grace_minutes)));
    if late_seconds <= 0 then return 0; end if;
    return ceil(rental.daily_price*rental.late_percent_per_day/100*ceil(late_seconds/86400));
end;
$$;

-- Supabase may give new public objects default grants; replace them explicitly.
do $$
declare t text;
begin
    foreach t in array array['staff_profiles','rental_settings','categories','equipment_types','equipment_units',
        'packages','package_components','customers','rentals','rental_lines','unit_assignments','inspections',
        'fines','payments','deposit_deductions','rental_events','audit_log'] loop
        execute format('revoke all on public.%I from public, anon, authenticated',t);
        execute format('grant select on public.%I to authenticated',t);
    end loop;
    foreach t in array array['rental_settings','categories','equipment_types','packages','package_components'] loop
        execute format('grant insert, update on public.%I to authenticated',t);
    end loop;
end;
$$;
revoke all on all tables in schema rantau_private from public,anon,authenticated;
revoke all on all functions in schema rantau_private from public,anon,authenticated;
grant execute on function rantau_private.is_staff(public.staff_role[]) to authenticated;
revoke all on function public.check_availability(bigint,timestamptz,timestamptz),
    public.save_customer(bigint,text,text,text),
    public.create_reservation(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint),
    public.cancel_reservation(bigint,text),public.expire_reservations(),public.calculate_late_fine(bigint,timestamptz)
    from public,anon,authenticated;
grant execute on function public.check_availability(bigint,timestamptz,timestamptz),
    public.save_customer(bigint,text,text,text),
    public.create_reservation(uuid,bigint,timestamptz,timestamptz,jsonb,public.payment_method,text,bigint),
    public.cancel_reservation(bigint,text),public.expire_reservations(),public.calculate_late_fine(bigint,timestamptz)
    to authenticated;

commit;
