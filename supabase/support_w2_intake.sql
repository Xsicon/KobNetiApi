-- KobNetiApi W2 — ticket intake context + PRD statuses
-- Apply after support_schema.sql
-- Schema: sominnercore

alter table sominnercore.support_tickets
  add column if not exists page_url text null;

alter table sominnercore.support_tickets
  add column if not exists account_id text null;

alter table sominnercore.support_tickets
  add column if not exists chat_session_id uuid null;

-- Expand status check to PRD set; keep legacy 'open' as synonym of 'new'
do $$
begin
  alter table sominnercore.support_tickets drop constraint if exists support_tickets_status_check;
exception when undefined_object then
  null;
end $$;

-- Drop any auto-named check on status (Postgres default naming)
do $$
declare
  cname text;
begin
  select con.conname into cname
  from pg_constraint con
  join pg_class rel on rel.oid = con.conrelid
  join pg_namespace nsp on nsp.oid = rel.relnamespace
  where nsp.nspname = 'sominnercore'
    and rel.relname = 'support_tickets'
    and con.contype = 'c'
    and pg_get_constraintdef(con.oid) ilike '%status%';
  if cname is not null then
    execute format('alter table sominnercore.support_tickets drop constraint %I', cname);
  end if;
end $$;

alter table sominnercore.support_tickets
  add constraint support_tickets_status_check
  check (status in ('new', 'open', 'in_progress', 'waiting', 'resolved', 'closed'));

alter table sominnercore.support_tickets
  alter column status set default 'new';
