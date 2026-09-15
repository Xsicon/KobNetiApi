-- Add KobNeti as an internal product so it appears in the project switcher
-- and staff can be assigned to it (Users → products).
-- Schema: sominnercore

insert into sominnercore.products
  (slug, display_name, product_type, status, support_tier, public_key, enabled)
values
  ('kobneti', 'KobNeti', 'internal_tool', 'active', 'enterprise', 'pk_kobneti_dev_public', true)
on conflict (slug) do update set
  display_name = excluded.display_name,
  product_type = excluded.product_type,
  public_key = excluded.public_key,
  enabled = excluded.enabled,
  updated_at = timezone('utc', now());
