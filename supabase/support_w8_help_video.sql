-- Help Center tutorial videos on /admin/help
-- Apply after support_w7_insights.sql (ops_help_articles)
-- Idempotent: safe to re-run

alter table sominnercore.ops_help_articles
  add column if not exists video_url text null;
