-- KobNetiApi W5 — Time tracking, approvals, payroll
-- Apply after support_w4_engineering.sql
-- Schema: sominnercore

create table if not exists sominnercore.ops_time_entries (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  user_id uuid null,
  user_name text not null default '',
  entry_type text not null
    check (entry_type in ('clock', 'manual')),
  clock_in timestamptz null,
  clock_out timestamptz null,
  minutes int null,
  ticket_id uuid null references sominnercore.support_tickets(id) on delete set null,
  eng_task_id uuid null references sominnercore.eng_tasks(id) on delete set null,
  notes text null,
  status text not null default 'approved'
    check (status in ('open', 'pending', 'approved', 'rejected', 'superseded')),
  supersedes_id uuid null,
  approval_id uuid null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_time_entries_tenant_user
  on sominnercore.ops_time_entries (tenant_id, user_id, created_at desc);
create index if not exists ix_ops_time_entries_tenant_status
  on sominnercore.ops_time_entries (tenant_id, status);

create table if not exists sominnercore.ops_approval_requests (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  request_type text not null
    check (request_type in ('time_edit', 'payroll')),
  status text not null default 'pending'
    check (status in ('pending', 'approved', 'rejected')),
  payload_json text not null default '{}',
  requester_user_id uuid null,
  requester_name text null,
  approver_user_id uuid null,
  approver_name text null,
  decided_at timestamptz null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_approvals_tenant_status
  on sominnercore.ops_approval_requests (tenant_id, status, created_at desc);

create table if not exists sominnercore.ops_pay_rates (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  staff_id uuid null,
  user_id uuid null,
  role text null,
  hourly_rate numeric(12,2) not null,
  currency text not null default 'USD',
  effective_from date not null default (timezone('utc', now())::date),
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_pay_rates_tenant
  on sominnercore.ops_pay_rates (tenant_id, effective_from desc);

create table if not exists sominnercore.ops_pay_periods (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  label text not null,
  starts_on date not null,
  ends_on date not null,
  status text not null default 'open'
    check (status in ('open', 'calculated', 'pending_approval', 'approved', 'exported')),
  total_minutes int not null default 0,
  total_amount numeric(14,2) not null default 0,
  currency text not null default 'USD',
  lines_json text not null default '[]',
  approval_id uuid null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_pay_periods_tenant
  on sominnercore.ops_pay_periods (tenant_id, starts_on desc);

revoke all on table sominnercore.ops_time_entries from anon, authenticated;
revoke all on table sominnercore.ops_approval_requests from anon, authenticated;
revoke all on table sominnercore.ops_pay_rates from anon, authenticated;
revoke all on table sominnercore.ops_pay_periods from anon, authenticated;
grant all on table sominnercore.ops_time_entries to service_role;
grant all on table sominnercore.ops_approval_requests to service_role;
grant all on table sominnercore.ops_pay_rates to service_role;
grant all on table sominnercore.ops_pay_periods to service_role;
alter table sominnercore.ops_time_entries enable row level security;
alter table sominnercore.ops_approval_requests enable row level security;
alter table sominnercore.ops_pay_rates enable row level security;
alter table sominnercore.ops_pay_periods enable row level security;
