begin;

create function public.pickup_reservation(
    p_request_id uuid,p_rental_id bigint,p_unit_ids bigint[],p_method public.payment_method,p_reference text default ''
) returns public.rentals language plpgsql security definer set search_path = '' as $$
declare actor uuid; result public.rentals; previous rantau_private.requests; body jsonb; rent_paid numeric; deposit_paid numeric;
begin
    actor:=rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    if p_request_id is null or p_rental_id is null or p_method is null or p_unit_ids is null then raise exception 'Data pengambilan belum lengkap.'; end if;
    body:=jsonb_build_object('rental',p_rental_id,'units',to_jsonb(p_unit_ids),'method',p_method,'reference',coalesce(p_reference,''));
    perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
    select * into previous from rantau_private.requests where request_id=p_request_id;
    if found then
        if previous.actor_id<>actor or previous.operation<>'pickup' or previous.payload<>body then raise exception 'ID permintaan sudah digunakan untuk data lain.'; end if;
        select * into strict result from public.rentals where id=previous.rental_id;
        return result;
    end if;
    select * into result from public.rentals where id=p_rental_id for update;
    if not found then raise exception 'Reservasi tidak ditemukan.'; end if;
    if result.state<>'reserved' then raise exception 'Reservasi ini tidak dapat diambil.'; end if;
    if statement_timestamp()<result.pickup_at or statement_timestamp()>result.pickup_deadline_at or statement_timestamp()>=result.due_at then
        raise exception 'Pengambilan harus dalam jadwal ambil, sebelum batas 12 jam, dan sebelum jadwal kembali.';
    end if;
    if cardinality(p_unit_ids)=0 or array_position(p_unit_ids,null) is not null
        or cardinality(p_unit_ids)<>(select count(distinct x) from unnest(p_unit_ids) x) then
        raise exception 'Pilih unit tanpa duplikasi.';
    end if;
    perform 1 from public.equipment_types where id in
        (select equipment_type_id from public.rental_lines where rental_id=p_rental_id) order by id for update;
    perform 1 from public.equipment_units where id=any(p_unit_ids) order by id for update;
    if (select count(*) from public.equipment_units where id=any(p_unit_ids) and state='ready')<>cardinality(p_unit_ids)
        or exists(select 1 from public.unit_assignments where unit_id=any(p_unit_ids) and released_at is null) then
        raise exception 'Semua unit harus siap, lolos QC, dan belum dipinjam.';
    end if;
    if exists (
        (select equipment_type_id,quantity::bigint from public.rental_lines where rental_id=p_rental_id
         except select equipment_type_id,count(*) from public.equipment_units where id=any(p_unit_ids) group by equipment_type_id)
        union all
        (select equipment_type_id,count(*) from public.equipment_units where id=any(p_unit_ids) group by equipment_type_id
         except select equipment_type_id,quantity::bigint from public.rental_lines where rental_id=p_rental_id)
    ) then raise exception 'Jenis dan jumlah unit harus sama dengan reservasi.'; end if;
    if exists(select 1 from public.unit_assignments a join public.rentals r on r.id=a.rental_id
        where a.unit_id=any(p_unit_ids) and a.released_at+make_interval(mins=>r.buffer_minutes)>statement_timestamp()) then
        raise exception 'Unit masih dalam jeda pemeriksaan dua jam.';
    end if;
    select coalesce(sum(amount),0) into rent_paid from public.payments where rental_id=p_rental_id and kind in ('down_payment','rent_payment');
    select coalesce(sum(amount),0) into deposit_paid from public.payments where rental_id=p_rental_id and kind='deposit_received';
    if rent_paid>result.rent_amount or deposit_paid>result.deposit_due then raise exception 'Saldo pembayaran perlu diperiksa.'; end if;
    if rent_paid<result.rent_amount then
        insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
        values(result.id,'rent_payment',result.rent_amount-rent_paid,p_method,coalesce(p_reference,''),actor);
    end if;
    if deposit_paid<result.deposit_due then
        insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
        values(result.id,'deposit_received',result.deposit_due-deposit_paid,p_method,coalesce(p_reference,''),actor);
    end if;
    insert into public.unit_assignments(rental_id,unit_id) select result.id,unnest(p_unit_ids);
    update public.equipment_units set state='rented' where id=any(p_unit_ids);
    update public.rentals set state='active',picked_up_at=statement_timestamp() where id=result.id returning * into result;
    insert into public.rental_events(rental_id,event_type,actor_id) values(result.id,'picked_up',actor);
    insert into rantau_private.requests values(p_request_id,actor,'pickup',body,result.id);
    return result;
end;
$$;

create function public.extend_rental(
    p_request_id uuid,p_rental_id bigint,p_due_at timestamptz,p_method public.payment_method,p_reference text default ''
) returns public.rentals language plpgsql security definer set search_path = '' as $$
declare actor uuid; result public.rentals; previous rantau_private.requests; body jsonb; line record; new_days integer; additional numeric;
begin
    actor:=rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    if p_request_id is null or p_rental_id is null or p_due_at is null or p_method is null then raise exception 'Data perpanjangan wajib lengkap.'; end if;
    body:=jsonb_build_object('rental',p_rental_id,'due',p_due_at,'method',p_method,'reference',coalesce(p_reference,''));
    perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
    select * into previous from rantau_private.requests where request_id=p_request_id;
    if found then
        if previous.actor_id<>actor or previous.operation<>'extend' or previous.payload<>body then raise exception 'ID permintaan sudah digunakan untuk data lain.'; end if;
        select * into strict result from public.rentals where id=previous.rental_id;
        return result;
    end if;
    select * into result from public.rentals where id=p_rental_id for update;
    if not found then raise exception 'Sewa tidak ditemukan.'; end if;
    if result.state<>'active' or p_due_at<=result.due_at then raise exception 'Hanya sewa aktif yang dapat diperpanjang ke waktu lebih akhir.'; end if;
    -- Post-due extensions require a separate settlement rule; never erase accrued lateness by moving due_at.
    if statement_timestamp()>result.due_at then raise exception 'Sewa sudah jatuh tempo; selesaikan keterlambatan dahulu. Perpanjangan tidak boleh menghapus denda.'; end if;
    perform 1 from public.equipment_types where id in
        (select equipment_type_id from public.rental_lines where rental_id=p_rental_id) order by id for update;
    for line in select * from public.rental_lines where rental_id=p_rental_id loop
        if rantau_private.available_quantity(line.equipment_type_id,result.pickup_at,p_due_at,result.buffer_minutes,result.id)<line.quantity then
            raise exception 'Perpanjangan bentrok dengan stok atau reservasi lain.';
        end if;
    end loop;
    new_days:=greatest(1,ceil(extract(epoch from(p_due_at-result.pickup_at))/86400)::integer);
    additional:=result.daily_price*new_days-result.rent_amount;
    if additional>0 then
        insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
        values(result.id,'rent_payment',additional,p_method,coalesce(p_reference,''),actor);
    end if;
    insert into public.rental_events(rental_id,event_type,actor_id,details)
        values(result.id,'extended',actor,jsonb_build_object('old_due',result.due_at,'new_due',p_due_at,'additional_rent',additional));
    update public.rentals set due_at=p_due_at,rental_days=new_days,rent_amount=daily_price*new_days where id=result.id returning * into result;
    insert into rantau_private.requests values(p_request_id,actor,'extend',body,result.id);
    return result;
end;
$$;

-- QC submits every assigned unit together. Damaged/lost charges are approved separately by an admin.
create function public.inspect_return(p_request_id uuid,p_rental_id bigint,p_checks jsonb)
returns public.rentals language plpgsql security definer set search_path = '' as $$
declare actor uuid; result public.rentals; previous rantau_private.requests; body jsonb; check_row record;
begin
    actor:=rantau_private.require_staff(array['super_admin','warehouse_qc']::public.staff_role[]);
    if p_request_id is null or p_rental_id is null or p_checks is null or jsonb_typeof(p_checks)<>'array' then raise exception 'Data pemeriksaan wajib berupa daftar lengkap.'; end if;
    body:=jsonb_build_object('rental',p_rental_id,'checks',p_checks);
    perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
    select * into previous from rantau_private.requests where request_id=p_request_id;
    if found then
        if previous.actor_id<>actor or previous.operation<>'inspect' or previous.payload<>body then raise exception 'ID permintaan sudah digunakan untuk data lain.'; end if;
        select * into strict result from public.rentals where id=previous.rental_id;
        return result;
    end if;
    select * into result from public.rentals where id=p_rental_id for update;
    if not found then raise exception 'Sewa tidak ditemukan.'; end if;
    if result.state<>'active' or result.returned_at is not null then raise exception 'Sewa tidak aktif atau sudah diperiksa.'; end if;
    if exists(select 1 from jsonb_to_recordset(p_checks) as x(unit_id bigint,condition text,charge numeric,notes text)
        where unit_id is null or condition is null or condition not in ('good','damaged','lost') or charge is null or charge<0 or charge<>trunc(charge)
            or (condition='good' and charge<>0) or (condition<>'good' and (charge<=0 or length(btrim(coalesce(notes,'')))=0))) then
        raise exception 'Kondisi, biaya rupiah, dan catatan pemeriksaan tidak valid; alat rusak/hilang wajib memiliki biaya.';
    end if;
    if jsonb_array_length(p_checks)<>(select count(*) from public.unit_assignments where rental_id=p_rental_id)
        or jsonb_array_length(p_checks)<>(select count(distinct unit_id) from jsonb_to_recordset(p_checks) as x(unit_id bigint))
        or exists(select 1 from jsonb_to_recordset(p_checks) as x(unit_id bigint)
            where not exists(select 1 from public.unit_assignments where rental_id=p_rental_id and unit_id=x.unit_id)) then
        raise exception 'Seluruh unit wajib diperiksa sekaligus; yang hilang harus dicatat sebagai hilang.';
    end if;
    perform 1 from public.equipment_types where id in
        (select equipment_type_id from public.rental_lines where rental_id=p_rental_id) order by id for update;
    for check_row in select * from jsonb_to_recordset(p_checks) as x(unit_id bigint,condition text,charge numeric,notes text) loop
        insert into public.inspections(assignment_id,condition,notes,proposed_charge,inspected_by)
            select id,check_row.condition,coalesce(check_row.notes,''),check_row.charge,actor
            from public.unit_assignments where rental_id=p_rental_id and unit_id=check_row.unit_id;
        update public.equipment_units set state=case check_row.condition when 'good' then 'ready'::public.equipment_state
            when 'damaged' then 'maintenance'::public.equipment_state else 'lost'::public.equipment_state end where id=check_row.unit_id;
    end loop;
    update public.unit_assignments set released_at=statement_timestamp() where rental_id=p_rental_id;
    update public.rentals set state='returned',returned_at=statement_timestamp() where id=p_rental_id returning * into result;
    insert into public.rental_events(rental_id,event_type,actor_id) values(result.id,'returned',actor);
    insert into rantau_private.requests values(p_request_id,actor,'inspect',body,result.id);
    return result;
end;
$$;

create function public.approve_return_charges(p_request_id uuid,p_rental_id bigint)
returns public.rentals language plpgsql security definer set search_path = '' as $$
declare actor uuid; result public.rentals; previous rantau_private.requests; body jsonb; check_row record; late numeric;
begin
    actor:=rantau_private.require_staff(array['super_admin']::public.staff_role[]);
    if p_request_id is null or p_rental_id is null then raise exception 'ID permintaan dan sewa wajib diisi.'; end if;
    body:=jsonb_build_object('rental',p_rental_id);
    perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
    select * into previous from rantau_private.requests where request_id=p_request_id;
    if found then
        if previous.actor_id<>actor or previous.operation<>'approve' or previous.payload<>body then raise exception 'ID permintaan sudah digunakan untuk data lain.'; end if;
        select * into strict result from public.rentals where id=previous.rental_id;
        return result;
    end if;
    select * into result from public.rentals where id=p_rental_id for update;
    if not found or result.state<>'returned' then raise exception 'Sewa harus diperiksa dan dikembalikan dahulu.'; end if;
    if exists(select 1 from public.rental_events where rental_id=p_rental_id and event_type='charges_approved') then raise exception 'Tagihan sudah disetujui.'; end if;
    for check_row in select i.* from public.inspections i join public.unit_assignments a on a.id=i.assignment_id
        where a.rental_id=p_rental_id and i.condition in ('damaged','lost') loop
        insert into public.fines(rental_id,inspection_id,kind,amount,reason,approved_by)
            values(result.id,check_row.id,case check_row.condition when 'lost' then 'loss' else 'damage' end,
                check_row.proposed_charge,check_row.notes,actor);
    end loop;
    late:=public.calculate_late_fine(result.id,result.returned_at);
    if late>0 then
        insert into public.fines(rental_id,kind,amount,reason,approved_by)
            values(result.id,'late',late,'Melewati toleransi satu jam; dihitung per hari.',actor);
    end if;
    insert into public.rental_events(rental_id,event_type,actor_id) values(result.id,'charges_approved',actor);
    insert into rantau_private.requests values(p_request_id,actor,'approve',body,result.id);
    return result;
end;
$$;

create function public.settle_return(p_request_id uuid,p_rental_id bigint,p_method public.payment_method,p_reference text default '')
returns public.rentals language plpgsql security definer set search_path = '' as $$
declare actor uuid; result public.rentals; previous rantau_private.requests; body jsonb; deposit numeric; fines numeric; remaining numeric; fine_row record; deduction numeric;
begin
    actor:=rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
    if p_request_id is null or p_rental_id is null or p_method is null then raise exception 'Data penyelesaian wajib lengkap.'; end if;
    body:=jsonb_build_object('rental',p_rental_id,'method',p_method,'reference',coalesce(p_reference,''));
    perform pg_advisory_xact_lock(hashtextextended(p_request_id::text,0));
    select * into previous from rantau_private.requests where request_id=p_request_id;
    if found then
        if previous.actor_id<>actor or previous.operation<>'settle' or previous.payload<>body then raise exception 'ID permintaan sudah digunakan untuk data lain.'; end if;
        select * into strict result from public.rentals where id=previous.rental_id;
        return result;
    end if;
    select * into result from public.rentals where id=p_rental_id for update;
    if not found or result.state<>'returned' then raise exception 'Sewa harus dikembalikan dahulu.'; end if;
    if not exists(select 1 from public.rental_events where rental_id=p_rental_id and event_type='charges_approved') then raise exception 'Tagihan harus disetujui Super Admin dahulu.'; end if;
    if exists(select 1 from public.rental_events where rental_id=p_rental_id and event_type='settled') then raise exception 'Transaksi sudah diselesaikan.'; end if;
    select coalesce(sum(amount),0) into deposit from public.payments where rental_id=p_rental_id and kind='deposit_received';
    select coalesce(sum(amount),0) into fines from public.fines where rental_id=p_rental_id;
    remaining:=deposit;
    for fine_row in select * from public.fines where rental_id=p_rental_id order by id loop
        deduction:=least(remaining,fine_row.amount);
        if deduction>0 then
            insert into public.deposit_deductions(fine_id,amount,recorded_by) values(fine_row.id,deduction,actor);
            remaining:=remaining-deduction;
        end if;
    end loop;
    if remaining>0 then
        insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
            values(result.id,'deposit_refund',remaining,p_method,coalesce(p_reference,''),actor);
    end if;
    if fines>deposit then
        insert into public.payments(rental_id,kind,amount,method,reference,recorded_by)
            values(result.id,'fine_payment',fines-deposit,p_method,coalesce(p_reference,''),actor);
    end if;
    insert into public.rental_events(rental_id,event_type,actor_id,details)
        values(result.id,'settled',actor,jsonb_build_object('fines',fines,'deposit_applied',least(fines,deposit),'refunded',remaining,'additional_paid',greatest(0,fines-deposit)));
    insert into rantau_private.requests values(p_request_id,actor,'settle',body,result.id);
    return result;
end;
$$;

do $$
declare f record;
begin
    for f in select p.oid::regprocedure as signature from pg_proc p join pg_namespace n on n.oid=p.pronamespace
        where n.nspname='public' and p.proname in ('pickup_reservation','extend_rental','inspect_return','approve_return_charges','settle_return') loop
        execute format('revoke all on function %s from public,anon,authenticated',f.signature);
        execute format('grant execute on function %s to authenticated',f.signature);
    end loop;
end;
$$;

commit;
