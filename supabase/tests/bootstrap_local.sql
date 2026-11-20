-- Local test database only. These stand-ins are supplied by Supabase in the cloud.
\set ON_ERROR_STOP on
select current_database() = 'rantau_validation' as correct_database \gset
\if :correct_database
\else
    \echo 'Refusing to install test auth outside rantau_validation.'
    \quit 1
\endif
do $$ begin if not exists(select 1 from pg_roles where rolname='anon') then create role anon nologin; end if; end $$;
do $$ begin if not exists(select 1 from pg_roles where rolname='authenticated') then create role authenticated nologin; end if; end $$;
create schema auth;
create table auth.users(id uuid primary key);
create function auth.uid() returns uuid language sql stable as $$
    select nullif(current_setting('request.jwt.claim.sub',true),'')::uuid;
$$;
grant usage on schema auth to authenticated,anon;
grant execute on function auth.uid() to authenticated,anon;

alter default privileges in schema public grant all on tables to anon,authenticated;
alter default privileges in schema public grant all on sequences to anon,authenticated;
alter default privileges in schema public grant execute on functions to anon,authenticated;
