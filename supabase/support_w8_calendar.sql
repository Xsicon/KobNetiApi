-- Calendar: optional location on ops events
-- Apply on sominnercore after support_w4_engineering.sql

alter table sominnercore.ops_calendar_events
  add column if not exists location text null;
