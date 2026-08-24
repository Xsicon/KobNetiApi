-- KobNetiApi W2 lifecycle — tags, timeline, SLA, eng task link
-- Apply after support_w2_intake.sql
-- Schema: sominnercore

alter table sominnercore.support_tickets
  add column if not exists tags text null;

alter table sominnercore.support_tickets
  add column if not exists sla_first_response_minutes int null;

alter table sominnercore.support_tickets
  add column if not exists first_response_due_at timestamptz null;

alter table sominnercore.support_tickets
  add column if not exists resolve_due_at timestamptz null;

alter table sominnercore.support_tickets
  add column if not exists eng_task_id uuid null;

create table if not exists sominnercore.support_ticket_events (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  ticket_id uuid not null references sominnercore.support_tickets(id) on delete cascade,
  event_type text not null,
  actor_name text null,
  detail text null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_ticket_events_ticket
  on sominnercore.support_ticket_events (tenant_id, ticket_id, created_at);

revoke all on table sominnercore.support_ticket_events from anon, authenticated;
grant all on table sominnercore.support_ticket_events to service_role;
alter table sominnercore.support_ticket_events enable row level security;
