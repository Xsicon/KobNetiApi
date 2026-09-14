-- KobNetiApi — Users hub: create staff tables if missing, then status / last-active / invites
-- Safe to run when staff_access.sql was never applied.
-- Schema: sominnercore

create schema if not exists sominnercore;
grant usage on schema sominnercore to anon, authenticated, service_role;

create table if not exists sominnercore.staff_profiles (
  id uuid primary key default gen_random_uuid(),
  user_id uuid null,
  email text not null unique,
  display_name text null,
  role text not null default 'support'
    check (role in ('admin', 'manager', 'engineer', 'support')),
  active boolean not null default true,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_staff_profiles_email
  on sominnercore.staff_profiles (email);

create table if not exists sominnercore.staff_product_access (
  id uuid primary key default gen_random_uuid(),
  staff_id uuid not null references sominnercore.staff_profiles(id) on delete cascade,
  product_slug text not null,
  created_at timestamptz not null default timezone('utc', now()),
  unique (staff_id, product_slug)
);

create index if not exists ix_staff_product_access_slug
  on sominnercore.staff_product_access (product_slug);

alter table sominnercore.staff_profiles
  add column if not exists status text not null default 'active';

alter table sominnercore.staff_profiles
  add column if not exists last_active_at timestamptz null;

alter table sominnercore.staff_profiles
  add column if not exists roles_json text not null default '[]';

update sominnercore.staff_profiles
set status = 'deactivated'
where active = false and coalesce(status, 'active') = 'active';

update sominnercore.staff_profiles
set roles_json = json_build_array(role)::text
where coalesce(roles_json, '') in ('', '[]');

do $$
begin
  if to_regclass('sominnercore.staff_profiles') is null then
    return;
  end if;

  if not exists (
    select 1
    from pg_constraint
    where conname = 'staff_profiles_status_check'
      and conrelid = to_regclass('sominnercore.staff_profiles')
  ) then
    alter table sominnercore.staff_profiles
      add constraint staff_profiles_status_check
      check (status in ('active', 'suspended', 'deactivated'));
  end if;
end $$;

create table if not exists sominnercore.staff_invites (
  id uuid primary key default gen_random_uuid(),
  staff_id uuid null references sominnercore.staff_profiles(id) on delete set null,
  email text not null,
  display_name text null,
  role text not null default 'support',
  roles_json text not null default '[]',
  product_slugs_json text not null default '[]',
  invited_by_email text null,
  created_at timestamptz not null default timezone('utc', now()),
  expires_at timestamptz not null,
  accepted_at timestamptz null,
  cancelled_at timestamptz null
);

create index if not exists ix_staff_invites_email
  on sominnercore.staff_invites (email);

create index if not exists ix_staff_invites_pending
  on sominnercore.staff_invites (accepted_at, cancelled_at, expires_at);

create unique index if not exists ux_staff_invites_open_email
  on sominnercore.staff_invites (lower(email))
  where accepted_at is null and cancelled_at is null;

revoke all on table sominnercore.staff_profiles from anon, authenticated;
revoke all on table sominnercore.staff_product_access from anon, authenticated;
revoke all on table sominnercore.staff_invites from anon, authenticated;
grant all on table sominnercore.staff_profiles to service_role;
grant all on table sominnercore.staff_product_access to service_role;
grant all on table sominnercore.staff_invites to service_role;
alter table sominnercore.staff_profiles enable row level security;
alter table sominnercore.staff_product_access enable row level security;
alter table sominnercore.staff_invites enable row level security;
