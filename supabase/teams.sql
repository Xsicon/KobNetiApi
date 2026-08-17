-- KobNetiApi — Teams + membership (W1.6)
-- Schema: sominnercore (unchanged)
-- Apply after staff_access.sql

create schema if not exists sominnercore;

create table if not exists sominnercore.teams (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  slug text not null unique,
  description text null,
  product_slug text null,
  active boolean not null default true,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_teams_product_slug
  on sominnercore.teams (product_slug);

create table if not exists sominnercore.team_members (
  id uuid primary key default gen_random_uuid(),
  team_id uuid not null references sominnercore.teams(id) on delete cascade,
  staff_id uuid not null references sominnercore.staff_profiles(id) on delete cascade,
  member_role text not null default 'member'
    check (member_role in ('lead', 'member')),
  created_at timestamptz not null default timezone('utc', now()),
  unique (team_id, staff_id)
);

create index if not exists ix_team_members_staff
  on sominnercore.team_members (staff_id);

revoke all on table sominnercore.teams from anon, authenticated;
revoke all on table sominnercore.team_members from anon, authenticated;
grant all on table sominnercore.teams to service_role;
grant all on table sominnercore.team_members to service_role;
alter table sominnercore.teams enable row level security;
alter table sominnercore.team_members enable row level security;
