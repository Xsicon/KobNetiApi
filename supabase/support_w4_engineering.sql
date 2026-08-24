-- KobNetiApi W4 — Engineering tasks, milestones, calendar events, GitHub cache
-- Apply after support_w3_incidents.sql
-- Schema: sominnercore

alter table sominnercore.products
  add column if not exists github_repo_url text null;

create table if not exists sominnercore.eng_tasks (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  task_number text not null,
  title text not null,
  description text null,
  task_type text not null default 'feature'
    check (task_type in ('feature', 'bug', 'chore', 'spike', 'incident')),
  status text not null default 'backlog'
    check (status in ('backlog', 'ready', 'in_progress', 'in_review', 'done', 'cancelled')),
  priority text not null default 'medium'
    check (priority in ('critical', 'high', 'medium', 'low')),
  estimate_points numeric(6,1) null,
  assignee_name text null,
  assignee_user_id uuid null,
  ticket_id uuid null references sominnercore.support_tickets(id) on delete set null,
  milestone_id uuid null,
  github_pr_url text null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, task_number)
);

create index if not exists ix_eng_tasks_tenant_status
  on sominnercore.eng_tasks (tenant_id, status);
create index if not exists ix_eng_tasks_ticket
  on sominnercore.eng_tasks (tenant_id, ticket_id);

create table if not exists sominnercore.eng_milestones (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  title text not null,
  description text null,
  status text not null default 'planned'
    check (status in ('planned', 'active', 'completed', 'cancelled')),
  target_date date null,
  start_date date null,
  sort_order int not null default 0,
  calendar_event_id uuid null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_eng_milestones_tenant_date
  on sominnercore.eng_milestones (tenant_id, target_date);

-- Soft FK for milestone_id on tasks (avoid circular create order)
do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'fk_eng_tasks_milestone'
  ) then
    alter table sominnercore.eng_tasks
      add constraint fk_eng_tasks_milestone
      foreign key (milestone_id) references sominnercore.eng_milestones(id) on delete set null;
  end if;
end $$;

-- W4.7 / W6.7 early: calendar events table for milestone dates
create table if not exists sominnercore.ops_calendar_events (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  title text not null,
  description text null,
  event_type text not null default 'milestone'
    check (event_type in ('milestone', 'reminder', 'meeting', 'other')),
  starts_at timestamptz not null,
  ends_at timestamptz null,
  source_entity_type text null,
  source_entity_id uuid null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_calendar_events_tenant_starts
  on sominnercore.ops_calendar_events (tenant_id, starts_at);

-- W4.9 read-only GitHub cache (never written to GitHub from ops API)
create table if not exists sominnercore.eng_github_cache (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  repo_url text not null,
  cache_kind text not null check (cache_kind in ('pulls', 'commits')),
  payload_json text not null default '[]',
  fetched_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, cache_kind)
);

revoke all on table sominnercore.eng_tasks from anon, authenticated;
revoke all on table sominnercore.eng_milestones from anon, authenticated;
revoke all on table sominnercore.ops_calendar_events from anon, authenticated;
revoke all on table sominnercore.eng_github_cache from anon, authenticated;
grant all on table sominnercore.eng_tasks to service_role;
grant all on table sominnercore.eng_milestones to service_role;
grant all on table sominnercore.ops_calendar_events to service_role;
grant all on table sominnercore.eng_github_cache to service_role;
alter table sominnercore.eng_tasks enable row level security;
alter table sominnercore.eng_milestones enable row level security;
alter table sominnercore.ops_calendar_events enable row level security;
alter table sominnercore.eng_github_cache enable row level security;
