-- KobNetiApi W3 — Incidents / escalation (Module 6)
-- Apply after support_w2_lifecycle.sql
-- Schema: sominnercore

create table if not exists sominnercore.support_incidents (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  incident_number text not null,
  title text not null,
  severity text not null default 'sev3'
    check (severity in ('sev1', 'sev2', 'sev3', 'sev4')),
  status text not null default 'open'
    check (status in ('open', 'investigating', 'mitigated', 'resolved', 'closed')),
  commander_name text null,
  commander_user_id uuid null,
  source_ticket_id uuid null references sominnercore.support_tickets(id) on delete set null,
  postmortem_notes text null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  resolved_at timestamptz null,
  unique (tenant_id, incident_number)
);

create index if not exists ix_support_incidents_tenant_status
  on sominnercore.support_incidents (tenant_id, status);

create table if not exists sominnercore.support_incident_events (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  incident_id uuid not null references sominnercore.support_incidents(id) on delete cascade,
  event_type text not null,
  actor_name text null,
  detail text null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_incident_events_incident
  on sominnercore.support_incident_events (tenant_id, incident_id, created_at);

revoke all on table sominnercore.support_incidents from anon, authenticated;
revoke all on table sominnercore.support_incident_events from anon, authenticated;
grant all on table sominnercore.support_incidents to service_role;
grant all on table sominnercore.support_incident_events to service_role;
alter table sominnercore.support_incidents enable row level security;
alter table sominnercore.support_incident_events enable row level security;
