-- KobNetiApi — Linked repositories per product (web app + API, etc.)
-- Apply after products_registry.sql
-- Schema: sominnercore

create table if not exists sominnercore.product_repos (
  id uuid primary key default gen_random_uuid(),
  product_slug text not null references sominnercore.products(slug) on delete cascade,
  repo_kind text not null
    check (repo_kind in ('web_app', 'api', 'mobile', 'internal')),
  title text not null,
  github_repo_url text not null,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  unique (product_slug, repo_kind)
);

create index if not exists ix_product_repos_slug
  on sominnercore.product_repos (product_slug);

-- GitHub cache: one row per tenant + linked repo + cache kind (pulls/commits)
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

revoke all on table sominnercore.product_repos from anon, authenticated;
grant all on table sominnercore.product_repos to service_role;
alter table sominnercore.product_repos enable row level security;

-- Seed MuuqWear web + API repos (idempotent; edit URLs in Platform / Engineering)
insert into sominnercore.product_repos (product_slug, repo_kind, title, github_repo_url)
values
  ('muuqwear', 'web_app', 'MuuqWear Web', 'https://github.com/kobneti/muuqwear-web'),
  ('muuqwear', 'api', 'MuuqWear API', 'https://github.com/kobneti/muuqwear-api')
on conflict (product_slug, repo_kind) do update set
  title = excluded.title,
  updated_at = timezone('utc', now());
