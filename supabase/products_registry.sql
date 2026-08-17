-- KobNetiApi — Product Registry (Module 3)
-- Schema: sominnercore (name unchanged)
-- Apply in Supabase SQL editor after support_schema.sql

create schema if not exists sominnercore;

grant usage on schema sominnercore to anon, authenticated, service_role;

create table if not exists sominnercore.products (
  id uuid primary key default gen_random_uuid(),
  slug text not null unique,                    -- tenant_id / product key (e.g. muuqwear)
  display_name text not null,
  product_type text not null default 'saas_app'
    check (product_type in ('public_website', 'saas_app', 'mobile_app', 'internal_tool')),
  status text not null default 'active'
    check (status in ('active', 'beta', 'deprecated')),
  support_tier text not null default 'standard'
    check (support_tier in ('standard', 'priority', 'enterprise')),
  public_key text not null unique,
  jwt_secret text null,                         -- prefer env override for secrets in prod
  upstream_api_base_url text null,
  public_help_center_url text null,
  enabled boolean not null default true,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index if not exists ix_products_enabled_slug
  on sominnercore.products (enabled, slug);

create index if not exists ix_products_public_key
  on sominnercore.products (public_key);

revoke all on table sominnercore.products from anon, authenticated;
grant all on table sominnercore.products to service_role;
alter table sominnercore.products enable row level security;

-- Seed first three products (idempotent)
insert into sominnercore.products
  (slug, display_name, product_type, status, support_tier, public_key, enabled)
values
  ('muuqwear', 'MuuqWear', 'saas_app', 'active', 'standard', 'pk_muuqwear_dev_public', true),
  ('salguri',  'Salguri',  'saas_app', 'active', 'standard', 'pk_salguri_dev_public',  true),
  ('gaarx',    'GaarX',    'saas_app', 'active', 'standard', 'pk_gaarx_dev_public',    true)
on conflict (slug) do update set
  display_name = excluded.display_name,
  public_key = excluded.public_key,
  enabled = excluded.enabled,
  updated_at = timezone('utc', now());
