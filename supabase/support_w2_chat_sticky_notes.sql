-- W2 — Agent sticky notes per chat session (ops-owned; works with bridged product-API sessions)

create table if not exists sominnercore.support_chat_sticky_notes (
  tenant_id text not null,
  session_id uuid primary key,
  agent_name text not null default '',
  reason_for_contact text not null default '',
  key_actions_taken jsonb not null default '[]'::jsonb,
  color_hex text not null default '#F29D68',
  pinned boolean not null default false,
  updated_at timestamptz not null default timezone('utc', now()),
  updated_by uuid null
);

create index if not exists ix_support_chat_sticky_notes_tenant
  on sominnercore.support_chat_sticky_notes (tenant_id, updated_at desc);

revoke all on table sominnercore.support_chat_sticky_notes from anon, authenticated;
grant all on table sominnercore.support_chat_sticky_notes to service_role;
alter table sominnercore.support_chat_sticky_notes enable row level security;
