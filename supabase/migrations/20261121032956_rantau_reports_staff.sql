create function rantau_private.read_report(p_kind text,p_from date,p_to date,p_status text,p_offset integer,p_limit integer) returns jsonb
language plpgsql stable security definer set search_path='' as $$
declare starts timestamptz; ends timestamptz; rows jsonb; summary jsonb; total bigint;
 receipts numeric; deposits numeric; refunds numeric; rent_refunds numeric; applied numeric; opening numeric; closing numeric;
begin
 perform rantau_private.require_staff(array['super_admin','front_office']::public.staff_role[]);
 if p_kind not in ('cash','rentals') or p_kind is null or p_from is null or p_to is null or p_to<p_from or p_offset is null or p_limit is null or p_offset<0 or p_limit<1 or p_limit>10000
  or p_status is null or p_status not in ('all','reserved','active','late','returned','cancelled','expired') then raise exception 'RG_REPORT_INPUT'; end if;
 starts:=p_from::timestamp at time zone 'Asia/Jakarta'; ends:=(p_to+1)::timestamp at time zone 'Asia/Jakarta';
 if p_kind='cash' then
  select count(*),coalesce(sum(amount) filter(where kind in ('down_payment','rent_payment','fine_payment')),0),
   coalesce(sum(amount) filter(where kind='deposit_received'),0),coalesce(sum(amount) filter(where kind='deposit_refund'),0),coalesce(sum(amount) filter(where kind='rent_refund'),0)
   into total,receipts,deposits,refunds,rent_refunds from public.payments where recorded_at>=starts and recorded_at<ends;
  select coalesce(sum(amount),0) into applied from public.deposit_deductions where recorded_at>=starts and recorded_at<ends;
  select coalesce(sum(case when kind='deposit_received' then amount when kind='deposit_refund' then -amount else 0 end),0) into opening from public.payments where recorded_at<starts;
  opening:=opening-(select coalesce(sum(amount),0) from public.deposit_deductions where recorded_at<starts);
  closing:=opening+deposits-refunds-applied;
  summary:=jsonb_build_object('rental_receipts',receipts,'deposit_received',deposits,'deposit_refund',refunds,'rent_refund',rent_refunds,
   'cash_net',receipts+deposits-refunds-rent_refunds,'deposit_applied',applied,'opening_deposit',opening,'closing_deposit',closing);
  select coalesce(jsonb_agg(to_jsonb(q) order by q.recorded_at,q.id),'[]') into rows from (
   select p.id,p.recorded_at,r.rental_number,p.kind,p.method,p.reference,
    case when p.kind in ('deposit_refund','rent_refund') then 0 else p.amount end as cash_in,
    case when p.kind in ('deposit_refund','rent_refund') then p.amount else 0 end as cash_out
   from public.payments p join public.rentals r on r.id=p.rental_id where p.recorded_at>=starts and p.recorded_at<ends
   order by p.recorded_at,p.id offset p_offset limit p_limit) q;
 else
  select count(*) into total from public.rental_overview r where r.pickup_at>=starts and r.pickup_at<ends and (p_status='all' or r.display_state=p_status);
  select jsonb_build_object('total',count(*),'reserved',count(*) filter(where display_state='reserved'),'active',count(*) filter(where display_state='active'),
   'late',count(*) filter(where display_state='late'),'returned',count(*) filter(where display_state='returned'),'cancelled',count(*) filter(where display_state='cancelled'),'expired',count(*) filter(where display_state='expired'))
   into summary from public.rental_overview r where r.pickup_at>=starts and r.pickup_at<ends and (p_status='all' or r.display_state=p_status);
  select coalesce(jsonb_agg(to_jsonb(q) order by q.pickup_at,q.id),'[]') into rows from (
   select r.id,r.rental_number,c.name as customer_name,r.pickup_at,r.due_at,r.display_state,r.rent_amount
   from public.rental_overview r join public.customers c on c.id=r.customer_id where r.pickup_at>=starts and r.pickup_at<ends and (p_status='all' or r.display_state=p_status)
   order by r.pickup_at,r.id offset p_offset limit p_limit) q;
 end if;
 return jsonb_build_object('kind',p_kind,'total',total,'summary',summary,'rows',rows,'snapshot_at',statement_timestamp());
end $$;
create function public.read_report(p_kind text,p_from date,p_to date,p_status text,p_offset integer,p_limit integer) returns jsonb
language sql stable security invoker set search_path='' as $$ select rantau_private.read_report(p_kind,p_from,p_to,p_status,p_offset,p_limit); $$;
revoke all on function public.read_report(text,date,date,text,integer,integer),rantau_private.read_report(text,date,date,text,integer,integer) from public,anon,authenticated;
grant execute on function public.read_report(text,date,date,text,integer,integer),rantau_private.read_report(text,date,date,text,integer,integer) to authenticated;

alter table public.staff_profiles add column row_version bigint not null default 1;
create trigger staff_version before update on public.staff_profiles for each row execute function rantau_private.bump_master_version();
create table rantau_private.staff_requests(request_id uuid primary key,actor_id uuid not null references public.staff_profiles(id),operation text not null,payload jsonb not null,target_id uuid,completed boolean not null default false,created_at timestamptz not null default now());
alter table rantau_private.staff_requests enable row level security;
revoke all on rantau_private.staff_requests from public,anon,authenticated;
create index staff_requests_actor_idx on rantau_private.staff_requests(actor_id);
create index staff_requests_target_idx on rantau_private.staff_requests(target_id);
create function rantau_private.list_staff(p_offset integer,p_limit integer) returns jsonb language plpgsql stable security definer set search_path='' as $$
declare rows jsonb;
begin
 perform rantau_private.require_staff(array['super_admin']::public.staff_role[]);
 if p_offset is null or p_limit is null or p_offset<0 or p_limit<1 or p_limit>100 then raise exception 'RG_STAFF_INPUT'; end if;
 select coalesce(jsonb_agg(to_jsonb(q) order by q.created_at,q.id),'[]') into rows from (select s.id,s.display_name,s.role,s.is_active,s.row_version,s.created_at,u.email from public.staff_profiles s join auth.users u on u.id=s.id order by s.created_at,s.id offset p_offset limit p_limit) q;
 return jsonb_build_object('rows',rows,'total',(select count(*) from public.staff_profiles));
end $$;
create function rantau_private.update_staff(p_request_id uuid,p_target_id uuid,p_version bigint,p_name text,p_role public.staff_role,p_active boolean) returns uuid
language plpgsql security definer set search_path='' as $$
declare actor uuid; target public.staff_profiles; previous rantau_private.staff_requests; body jsonb;
begin
 actor:=rantau_private.require_staff(array['super_admin']::public.staff_role[]);
 if p_request_id is null or p_target_id is null or p_role is null or p_active is null or length(btrim(coalesce(p_name,'')))=0 then raise exception 'RG_STAFF_INPUT'; end if;
 body:=jsonb_build_object('target',p_target_id,'version',p_version,'name',btrim(p_name),'role',p_role,'active',p_active);
 perform pg_advisory_xact_lock(hashtextextended('rantau.staff',0));
 select * into previous from rantau_private.staff_requests where request_id=p_request_id;
 if found then
  if previous.actor_id<>actor or previous.operation<>'update' or previous.payload<>body then raise exception 'RG_STAFF_REQUEST'; end if; return previous.target_id;
 end if;
 select * into target from public.staff_profiles where id=p_target_id for update;
 if not found or p_version is distinct from target.row_version then raise exception 'RG_STAFF_VERSION'; end if;
 if p_target_id=actor and (not p_active or p_role<>'super_admin') then raise exception 'RG_STAFF_SELF'; end if;
 if target.role='super_admin' and target.is_active and (not p_active or p_role<>'super_admin') and (select count(*) from public.staff_profiles where role='super_admin' and is_active)<=1 then raise exception 'RG_STAFF_LAST_ADMIN'; end if;
 update public.staff_profiles set display_name=btrim(p_name),role=p_role,is_active=p_active where id=target.id;
 insert into rantau_private.staff_requests(request_id,actor_id,operation,payload,target_id,completed) values(p_request_id,actor,'update',body,target.id,true);
 return target.id;
end $$;
create function rantau_private.staff_admin_prepare(p_request_id uuid,p_action text,p_target_id uuid,p_values jsonb) returns jsonb
language plpgsql security definer set search_path='' as $$
declare actor uuid; previous rantau_private.staff_requests; body jsonb; target uuid; input_email text; display_name text; role public.staff_role;
begin
 actor:=rantau_private.require_staff(array['super_admin']::public.staff_role[]);
 if p_request_id is null or p_action is null or p_action not in ('create','password') then raise exception 'RG_STAFF_INPUT'; end if;
 if p_action='create' then
  input_email:=lower(btrim(coalesce(p_values->>'email',''))); display_name:=btrim(coalesce(p_values->>'display_name','')); role:=(p_values->>'role')::public.staff_role;
  if input_email='' or position('@' in input_email)<2 or display_name='' or role is null then raise exception 'RG_STAFF_INPUT'; end if;
  body:=jsonb_build_object('email',input_email,'display_name',display_name,'role',role);
 else
  if p_target_id is null or not exists(select 1 from public.staff_profiles where id=p_target_id) then raise exception 'RG_STAFF_INPUT'; end if;
  body:=jsonb_build_object('target',p_target_id);
 end if;
 perform pg_advisory_xact_lock(hashtextextended('rantau.staff',0));
 select * into previous from rantau_private.staff_requests where request_id=p_request_id for update;
 if found then
  if previous.actor_id<>actor or previous.operation<>p_action or previous.payload<>body then raise exception 'RG_STAFF_REQUEST'; end if;
  if previous.completed then return jsonb_build_object('done',true,'user_id',previous.target_id); end if;
 else
  insert into rantau_private.staff_requests(request_id,actor_id,operation,payload,target_id) values(p_request_id,actor,p_action,body,p_target_id);
 end if;
 if p_action='create' then
  select id into target from auth.users where lower(auth.users.email)=input_email;
  if found and not exists(select 1 from auth.users u where u.id=target and u.raw_app_meta_data->>'rantau_creation_request'=p_request_id::text and u.raw_app_meta_data->>'rantau_created_by'=actor::text) then raise exception 'RG_STAFF_EMAIL'; end if;
 else target:=p_target_id; end if;
 return jsonb_build_object('done',false,'user_id',target);
end $$;
create function rantau_private.staff_admin_finish(p_request_id uuid,p_actor uuid) returns jsonb
language plpgsql security definer set search_path='' as $$
declare request rantau_private.staff_requests; target uuid;
begin
 perform pg_advisory_xact_lock(hashtextextended('rantau.staff',0));
 select * into request from rantau_private.staff_requests where request_id=p_request_id for update;
 if not found or request.actor_id<>p_actor or not exists(select 1 from public.staff_profiles where id=p_actor and role='super_admin' and is_active) then raise exception 'RG_STAFF_ACCESS' using errcode='42501'; end if;
 if request.completed then return jsonb_build_object('user_id',request.target_id); end if;
 if request.operation='create' then
  select id into target from auth.users u where lower(u.email)=request.payload->>'email' and u.raw_app_meta_data->>'rantau_creation_request'=p_request_id::text and u.raw_app_meta_data->>'rantau_created_by'=p_actor::text;
  if not found then raise exception 'RG_STAFF_PENDING'; end if;
  insert into public.staff_profiles(id,display_name,role,is_active) values(target,request.payload->>'display_name',(request.payload->>'role')::public.staff_role,true);
 elsif request.operation='password' then target:=request.target_id;
 else raise exception 'RG_STAFF_INPUT'; end if;
 update rantau_private.staff_requests set target_id=target,completed=true where request_id=p_request_id;
 insert into public.audit_log(table_name,row_id,action,actor_id,after_data) values('staff_profiles',target::text,case request.operation when 'create' then 'STAFF_CREATED' else 'PASSWORD_CHANGED' end,p_actor,jsonb_build_object('request_id',p_request_id));
 return jsonb_build_object('user_id',target);
end $$;
create function public.list_staff(p_offset integer,p_limit integer) returns jsonb language sql stable security invoker set search_path='' as $$ select rantau_private.list_staff(p_offset,p_limit); $$;
create function public.update_staff(p_request_id uuid,p_target_id uuid,p_version bigint,p_name text,p_role public.staff_role,p_active boolean) returns uuid language sql security invoker set search_path='' as $$ select rantau_private.update_staff(p_request_id,p_target_id,p_version,p_name,p_role,p_active); $$;
create function public.staff_admin_prepare(p_request_id uuid,p_action text,p_target_id uuid,p_values jsonb) returns jsonb language sql security invoker set search_path='' as $$ select rantau_private.staff_admin_prepare(p_request_id,p_action,p_target_id,p_values); $$;
create function public.staff_admin_finish(p_request_id uuid,p_actor uuid) returns jsonb language sql security invoker set search_path='' as $$ select rantau_private.staff_admin_finish(p_request_id,p_actor); $$;
revoke all on function public.list_staff(integer,integer),rantau_private.list_staff(integer,integer),public.update_staff(uuid,uuid,bigint,text,public.staff_role,boolean),rantau_private.update_staff(uuid,uuid,bigint,text,public.staff_role,boolean),public.staff_admin_prepare(uuid,text,uuid,jsonb),rantau_private.staff_admin_prepare(uuid,text,uuid,jsonb),public.staff_admin_finish(uuid,uuid),rantau_private.staff_admin_finish(uuid,uuid) from public,anon,authenticated;
grant execute on function public.list_staff(integer,integer),rantau_private.list_staff(integer,integer),public.update_staff(uuid,uuid,bigint,text,public.staff_role,boolean),rantau_private.update_staff(uuid,uuid,bigint,text,public.staff_role,boolean),public.staff_admin_prepare(uuid,text,uuid,jsonb),rantau_private.staff_admin_prepare(uuid,text,uuid,jsonb) to authenticated;
grant usage on schema rantau_private to service_role;
grant execute on function public.staff_admin_finish(uuid,uuid),rantau_private.staff_admin_finish(uuid,uuid) to service_role;
