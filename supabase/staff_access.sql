-- KobNetiApi — Staff profiles + product assignments (Modules 1–2)
-- Schema: sominnercore (unchanged)
-- Apply after products_registry.sql

create schema if not exists sominnercore;

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

revoke all on table sominnercore.staff_profiles from anon, authenticated;
revoke all on table sominnercore.staff_product_access from anon, authenticated;
grant all on table sominnercore.staff_profiles to service_role;
grant all on table sominnercore.staff_product_access to service_role;
alter table sominnercore.staff_profiles enable row level security;
alter table sominnercore.staff_product_access enable row level security;
