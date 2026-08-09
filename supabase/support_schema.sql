-- Som Inner Core — multi-tenant Support module
-- Schema: sominnercore
-- Apply in the Supabase SQL editor AFTER rls_policies.sql (or ensure schema exists).
--
-- Access model:
--   Sominnercore.SupportApi uses the service_role key and enforces tenant_id in app code.
--   Anon/authenticated must NOT have write access to these tables.
--   Deny-by-default RLS is enabled for defense in depth (no permissive policies for anon).

create schema if not exists sominnercore;

grant usage on schema sominnercore to anon, authenticated, service_role;

-- ─── Chat ────────────────────────────────────────────────────

create table if not exists sominnercore.support_chat_sessions (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  external_customer_id uuid null,
  guest_name text null,
  guest_email text null,
  status text not null default 'active' check (status in ('active', 'closed')),
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  closed_at timestamptz null
);

create index if not exists ix_support_chat_sessions_tenant_status
  on sominnercore.support_chat_sessions (tenant_id, status);
create index if not exists ix_support_chat_sessions_tenant_updated
  on sominnercore.support_chat_sessions (tenant_id, updated_at desc);

create table if not exists sominnercore.support_chat_messages (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  session_id uuid not null references sominnercore.support_chat_sessions(id) on delete cascade,
  sender_type text not null check (sender_type in ('customer', 'admin')),
  sender_id uuid null,
  sender_name text null,
  message text not null,
  is_read boolean not null default false,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_chat_messages_tenant_session
  on sominnercore.support_chat_messages (tenant_id, session_id, created_at);

-- ─── Tickets ─────────────────────────────────────────────────

create table if not exists sominnercore.support_tickets (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  ticket_number text not null,
  name text not null,
  email text not null,
  category text not null,
  subject text not null,
  message text not null,
  priority text not null default 'normal' check (priority in ('high', 'normal', 'low')),
  status text not null default 'open' check (status in ('open', 'in_progress', 'resolved')),
  team text null,
  assigned_to uuid null,
  assigned_to_name text null,
  first_response_at timestamptz null,
  external_customer_id uuid null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, ticket_number)
);

create index if not exists ix_support_tickets_tenant_status
  on sominnercore.support_tickets (tenant_id, status);
create index if not exists ix_support_tickets_tenant_created
  on sominnercore.support_tickets (tenant_id, created_at desc);

create table if not exists sominnercore.support_ticket_replies (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  ticket_id uuid not null references sominnercore.support_tickets(id) on delete cascade,
  sender_type text not null check (sender_type in ('customer', 'agent')),
  sender_name text null,
  message text not null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_ticket_replies_tenant_ticket
  on sominnercore.support_ticket_replies (tenant_id, ticket_id, created_at);

-- ─── Knowledge base ──────────────────────────────────────────

create table if not exists sominnercore.support_kb_articles (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  title text not null,
  category text not null,
  content text not null default '',
  status text not null default 'draft' check (status in ('draft', 'published')),
  hero_image_url text null,
  view_count int not null default 0,
  helpful_count int not null default 0,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  published_at timestamptz null
);

create index if not exists ix_support_kb_articles_tenant_status
  on sominnercore.support_kb_articles (tenant_id, status);
create index if not exists ix_support_kb_articles_tenant_created
  on sominnercore.support_kb_articles (tenant_id, created_at desc);

create table if not exists sominnercore.support_kb_article_steps (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  article_id uuid not null references sominnercore.support_kb_articles(id) on delete cascade,
  sort_order int not null default 0,
  detail text not null default '',
  image_url text null
);

create index if not exists ix_support_kb_steps_tenant_article
  on sominnercore.support_kb_article_steps (tenant_id, article_id, sort_order);

create table if not exists sominnercore.support_kb_article_comments (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  article_id uuid not null references sominnercore.support_kb_articles(id) on delete cascade,
  author_name text not null,
  body text not null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_kb_comments_tenant_article
  on sominnercore.support_kb_article_comments (tenant_id, article_id, created_at);

create table if not exists sominnercore.support_kb_article_votes (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  article_id uuid not null references sominnercore.support_kb_articles(id) on delete cascade,
  voter_key text not null,
  vote text not null check (vote in ('like', 'dislike')),
  created_at timestamptz not null default timezone('utc', now()),
  unique (tenant_id, article_id, voter_key)
);

create index if not exists ix_support_kb_votes_tenant_article
  on sominnercore.support_kb_article_votes (tenant_id, article_id);

-- ─── Macros + uploads ────────────────────────────────────────

create table if not exists sominnercore.support_macros (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  title text not null,
  body text not null,
  category text null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_macros_tenant
  on sominnercore.support_macros (tenant_id, updated_at desc);

create table if not exists sominnercore.support_uploads (
  id uuid primary key default gen_random_uuid(),
  tenant_id text not null,
  path text not null,
  public_url text not null,
  content_type text null,
  size_bytes bigint null,
  created_by uuid null,
  created_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_support_uploads_tenant
  on sominnercore.support_uploads (tenant_id, created_at desc);

-- ─── Grants: service_role only for Support tables ────────────

revoke all on table sominnercore.support_chat_sessions from anon, authenticated;
revoke all on table sominnercore.support_chat_messages from anon, authenticated;
revoke all on table sominnercore.support_tickets from anon, authenticated;
revoke all on table sominnercore.support_ticket_replies from anon, authenticated;
revoke all on table sominnercore.support_kb_articles from anon, authenticated;
revoke all on table sominnercore.support_kb_article_steps from anon, authenticated;
revoke all on table sominnercore.support_kb_article_comments from anon, authenticated;
revoke all on table sominnercore.support_kb_article_votes from anon, authenticated;
revoke all on table sominnercore.support_macros from anon, authenticated;
revoke all on table sominnercore.support_uploads from anon, authenticated;

grant all on table sominnercore.support_chat_sessions to service_role;
grant all on table sominnercore.support_chat_messages to service_role;
grant all on table sominnercore.support_tickets to service_role;
grant all on table sominnercore.support_ticket_replies to service_role;
grant all on table sominnercore.support_kb_articles to service_role;
grant all on table sominnercore.support_kb_article_steps to service_role;
grant all on table sominnercore.support_kb_article_comments to service_role;
grant all on table sominnercore.support_kb_article_votes to service_role;
grant all on table sominnercore.support_macros to service_role;
grant all on table sominnercore.support_uploads to service_role;

-- Deny-by-default RLS (no policies for anon/authenticated → blocked even if grants change)
alter table sominnercore.support_chat_sessions enable row level security;
alter table sominnercore.support_chat_messages enable row level security;
alter table sominnercore.support_tickets enable row level security;
alter table sominnercore.support_ticket_replies enable row level security;
alter table sominnercore.support_kb_articles enable row level security;
alter table sominnercore.support_kb_article_steps enable row level security;
alter table sominnercore.support_kb_article_comments enable row level security;
alter table sominnercore.support_kb_article_votes enable row level security;
alter table sominnercore.support_macros enable row level security;
alter table sominnercore.support_uploads enable row level security;

-- service_role bypasses RLS by default in Supabase.
