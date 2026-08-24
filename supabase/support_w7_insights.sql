-- KobNetiApi W7 — Insights, platform help, internal chat, assets
-- Apply after support_w6_platform_glue.sql
-- Schema: sominnercore

create table if not exists sominnercore.ops_help_articles (
  id uuid primary key default gen_random_uuid(),
  slug text not null unique,
  title text not null,
  body text not null default '',
  category text not null default 'general',
  status text not null default 'published'
    check (status in ('draft', 'published', 'archived')),
  sort_order int not null default 0,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_help_articles_status
  on sominnercore.ops_help_articles (status, sort_order);

create table if not exists sominnercore.ops_report_runs (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  report_type text not null
    check (report_type in ('tickets', 'time', 'payroll', 'audit')),
  label text not null,
  params_json text not null default '{}',
  row_count int not null default 0,
  csv_content text not null default '',
  created_by_name text null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_report_runs_tenant
  on sominnercore.ops_report_runs (tenant_id, created_at desc);

create table if not exists sominnercore.ops_im_channels (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  name text not null,
  channel_type text not null default 'channel'
    check (channel_type in ('channel', 'dm')),
  created_by uuid null,
  created_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, name, channel_type)
);

create table if not exists sominnercore.ops_im_messages (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  channel_id uuid not null references sominnercore.ops_im_channels(id) on delete cascade,
  sender_user_id uuid null,
  sender_name text not null default '',
  body text not null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_im_messages_channel
  on sominnercore.ops_im_messages (tenant_id, channel_id, created_at);

create table if not exists sominnercore.ops_assets (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  name text not null,
  asset_type text not null default 'hardware'
    check (asset_type in ('hardware', 'license', 'other')),
  serial_or_key text null,
  status text not null default 'available'
    check (status in ('available', 'assigned', 'retired')),
  assigned_user_id uuid null,
  assigned_user_name text null,
  renewal_date date null,
  notes text null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_ops_assets_tenant_status
  on sominnercore.ops_assets (tenant_id, status, renewal_date);

-- Seed a few platform help articles (idempotent)
insert into sominnercore.ops_help_articles (slug, title, body, category, status, sort_order)
values
  ('getting-started', 'Getting started with KobNeti',
   'Use the product switcher to pick a brand, then open Support, Engineering, or People Ops.',
   'general', 'published', 1),
  ('support-hub', 'Support Hub',
   'Live chat, tickets, incidents, and product KB live under Support Hub. Switch products from the sidebar.',
   'support', 'published', 2),
  ('time-payroll', 'Time & payroll',
   'Clock in/out under Time & Approvals. Payroll runs use approved time only.',
   'people', 'published', 3)
on conflict (slug) do update set
  title = excluded.title,
  body = excluded.body,
  updated_at = timezone('utc', now());

revoke all on table sominnercore.ops_help_articles from anon, authenticated;
revoke all on table sominnercore.ops_report_runs from anon, authenticated;
revoke all on table sominnercore.ops_im_channels from anon, authenticated;
revoke all on table sominnercore.ops_im_messages from anon, authenticated;
revoke all on table sominnercore.ops_assets from anon, authenticated;
grant all on table sominnercore.ops_help_articles to service_role;
grant all on table sominnercore.ops_report_runs to service_role;
grant all on table sominnercore.ops_im_channels to service_role;
grant all on table sominnercore.ops_im_messages to service_role;
grant all on table sominnercore.ops_assets to service_role;
alter table sominnercore.ops_help_articles enable row level security;
alter table sominnercore.ops_report_runs enable row level security;
alter table sominnercore.ops_im_channels enable row level security;
alter table sominnercore.ops_im_messages enable row level security;
alter table sominnercore.ops_assets enable row level security;
