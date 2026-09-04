-- Quick patch: add repo_key to eng_github_cache for multi-repo GitHub activity.
-- Run in Supabase SQL editor if you see: column eng_github_cache.repo_key does not exist
-- Full linked-repos setup: also run product_repos.sql

alter table sominnercore.eng_github_cache
  add column if not exists repo_key text not null default 'web_app';

do $$
begin
  if exists (
    select 1 from pg_constraint
    where conname = 'eng_github_cache_tenant_id_cache_kind_key'
  ) then
    alter table sominnercore.eng_github_cache
      drop constraint eng_github_cache_tenant_id_cache_kind_key;
  end if;
end $$;

create unique index if not exists ux_eng_github_cache_tenant_repo_kind
  on sominnercore.eng_github_cache (tenant_id, repo_key, cache_kind);
