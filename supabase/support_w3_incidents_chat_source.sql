-- KobNetiApi W3 — link incidents to live chat escalations (no ticket required)
-- Apply after support_w3_incidents.sql

alter table sominnercore.support_incidents
  add column if not exists source_chat_session_id uuid null;

create index if not exists ix_support_incidents_chat_source
  on sominnercore.support_incidents (tenant_id, source_chat_session_id)
  where source_chat_session_id is not null;
