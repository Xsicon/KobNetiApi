-- KobNetiApi W6 — Audit, notifications, files, integrations
-- Apply after support_w5_people_ops.sql
-- Schema: sominnercore
-- Note: ops_calendar_events already exists from W4

create table if not exists sominnercore.ops_audit_events (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  actor_user_id uuid null,
  actor_name text null,
  action text not null,
  entity_type text not null,
  entity_id text null,
  before_json text null,
  after_json text null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_audit_events_tenant_created
  on sominnercore.ops_audit_events (tenant_id, created_at desc);
create index if not exists ix_ops_audit_events_entity
  on sominnercore.ops_audit_events (tenant_id, entity_type, entity_id);

create table if not exists sominnercore.ops_notifications (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  user_id uuid null,
  user_name text null,
  channel text not null default 'in_app',
  title text not null,
  body text null,
  link_url text null,
  source_type text null,
  source_id text null,
  read_at timestamptz null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_notifications_unread
  on sominnercore.ops_notifications (tenant_id, user_id, read_at, created_at desc);

create table if not exists sominnercore.ops_notification_preferences (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  user_id uuid not null,
  assign_enabled boolean not null default true,
  approval_enabled boolean not null default true,
  escalation_enabled boolean not null default true,
  reminder_enabled boolean not null default true,
  updated_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, user_id)
);

create table if not exists sominnercore.ops_files (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  folder_path text not null default '/',
  file_name text not null,
  content_type text null,
  size_bytes bigint null,
  storage_path text not null,
  public_url text null,
  created_by uuid null,
  created_by_name text null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_files_tenant_folder
  on sominnercore.ops_files (tenant_id, folder_path, created_at desc);

create table if not exists sominnercore.ops_integrations (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  provider text not null
    check (provider in ('github', 'email', 'slack', 'other')),
  display_name text not null,
  status text not null default 'disconnected'
    check (status in ('connected', 'disconnected')),
  config_json text not null default '{}',
  connected_at timestamptz null,
  disconnected_at timestamptz null,
  updated_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, provider)
);

create table if not exists sominnercore.ops_integration_secrets (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  provider text not null,
  secret_key text not null,
  ciphertext text not null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, provider, secret_key)
);

revoke all on table sominnercore.ops_audit_events from anon, authenticated;
revoke all on table sominnercore.ops_notifications from anon, authenticated;
revoke all on table sominnercore.ops_notification_preferences from anon, authenticated;
revoke all on table sominnercore.ops_files from anon, authenticated;
revoke all on table sominnercore.ops_integrations from anon, authenticated;
revoke all on table sominnercore.ops_integration_secrets from anon, authenticated;
grant all on table sominnercore.ops_audit_events to service_role;
grant all on table sominnercore.ops_notifications to service_role;
grant all on table sominnercore.ops_notification_preferences to service_role;
grant all on table sominnercore.ops_files to service_role;
grant all on table sominnercore.ops_integrations to service_role;
grant all on table sominnercore.ops_integration_secrets to service_role;
alter table sominnercore.ops_audit_events enable row level security;
alter table sominnercore.ops_notifications enable row level security;
alter table sominnercore.ops_notification_preferences enable row level security;
alter table sominnercore.ops_files enable row level security;
alter table sominnercore.ops_integrations enable row level security;
alter table sominnercore.ops_integration_secrets enable row level security;
