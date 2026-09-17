-- Resource Management inventory
-- Schema: sominnercore
-- Idempotent: replaces only seed rows (notes like 'seed:assets%') plus the original a55e7000 ids
-- Assignees come from staff_profiles (prefers admin@sominnercore.com) — no invented employees.
-- Inserts the same 34 assets for every enabled product so Resource Management shows them
-- on the desk you are actually viewing (product switcher is hidden on this page).

delete from sominnercore.ops_assets
where notes like 'seed:assets%'
   or (id >= 'a55e7000-0000-4000-8000-000000000001'::uuid
       and id <= 'a55e7000-0000-4000-8000-000000000034'::uuid);

do $$
declare
  v_user_id uuid;
  v_name text;
  v_email text;
  v_tenant text;
  v_tenant_n int := 0;
  rec record;
  v_i int;
  v_id uuid;
  v_type text;
  v_status text;
  v_serial text;
  v_user uuid;
  v_user_name text;
begin
  select coalesce(sp.user_id, sp.id),
         coalesce(nullif(btrim(sp.display_name), ''),
                  initcap(replace(split_part(sp.email, '@', 1), '.', ' '))),
         sp.email
    into v_user_id, v_name, v_email
  from sominnercore.staff_profiles sp
  where coalesce(sp.active, true)
    and coalesce(sp.status, 'active') = 'active'
    and sp.email not ilike '%@sominnercore.local'
  order by
    case when sp.email ilike 'admin@sominnercore.com' then 0 else 1 end,
    sp.created_at
  limit 1;

  if v_user_id is null then
    raise exception 'Asset seed aborted — no active staff_profiles row found';
  end if;

  for v_tenant in
    select slug
    from sominnercore.products
    where coalesce(enabled, true)
    order by slug
  loop
    v_tenant_n := v_tenant_n + 1;

    for rec in
      select *
      from (values
        (1,  'MacBook Pro 16"',        'hardware', 'ABC123',   'available',  1460, 'warranty',  8),
        (2,  'MacBook Pro 14"',        'hardware', 'DEF456',   'assigned',    480, 'warranty', 40),
        (3,  'MacBook Air',            'hardware', 'JKL012',   'retired',    -800, 'warranty', 400),
        (4,  'Dell Monitor',           'hardware', 'MNO345',   'available',   540, 'warranty', 70),
        (5,  'Logitech Keyboard',      'hardware', 'PQR678',   'assigned',    235, null,        6),
        (6,  'ThinkPad X1 Carbon',     'hardware', 'TPX1001',  'assigned',     40, 'warranty', 55),
        (7,  'Dell XPS 15',            'hardware', 'XPS1502',  'assigned',    500, 'warranty', 90),
        (8,  'iPad Pro 12.9"',         'hardware', 'IPAD129',  'assigned',    600, 'warranty', 110),
        (9,  'iPhone 15',              'hardware', 'IP15A11',  'assigned',    400, 'warranty', 95),
        (10, 'Magic Keyboard',         'hardware', 'MK8821',   'available',  null::int, null,  120),
        (11, 'Logitech MX Master 3S',  'hardware', 'MX3S019',  'assigned',   null::int, null,   88),
        (12, 'Dell WD19 Dock',         'hardware', 'WD19A22',  'assigned',    300, 'warranty', 77),
        (13, 'Sony WH-1000XM5',        'hardware', 'SNYXM5',   'assigned',   null::int, null,   64),
        (14, 'LG UltraFine 27"',       'hardware', 'LG27UF',   'assigned',    700, 'warranty', 130),
        (15, 'Keychron K2',            'hardware', 'KC2B11',   'available',  null::int, null,  150),
        (16, 'Raspberry Pi 5',         'hardware', 'RPI5A8',   'assigned',   null::int, null,   44),
        (17, 'USB-C Hub',              'hardware', 'HUB7C2',   'available',  null::int, null,  200),
        (18, 'Anker 747 Charger',      'hardware', 'ANK747',   'assigned',   null::int, null,   33),
        (19, 'YubiKey 5C',             'hardware', 'YUB5C01',  'assigned',    900, null,        22),
        (20, 'HP LaserJet',            'hardware', 'HP-LJ4',   'assigned',    200, 'warranty', 210),
        (21, 'Logitech C920',          'hardware', 'C920-11',  'assigned',   null::int, null,  180),
        (22, 'CalDigit TS4',           'hardware', 'TS4-009',  'assigned',    450, 'warranty', 160),
        (23, 'Adobe Creative Cloud',   'license',  null::text, 'assigned',     44, null,        28),
        (24, 'Figma Pro',              'license',  null,       'assigned',     58, null,        31),
        (25, 'GitHub Copilot',         'license',  null,       'available',   110, null,         4),
        (26, 'Jira Enterprise',        'license',  null,       'assigned',    200, null,        75),
        (27, 'Slack Pro',              'license',  null,       'assigned',    300, null,        82),
        (28, '1Password Business',     'license',  null,       'assigned',    180, null,        61),
        (29, 'Notion Team',            'license',  null,       'assigned',    220, null,        49),
        (30, 'Linear',                 'license',  null,       'assigned',    160, null,        37),
        (31, 'Zoom Workplace',         'license',  null,       'assigned',    120, null,        18),
        (32, 'Microsoft 365',          'license',  null,       'assigned',    250, null,        99),
        (33, 'Grammarly Business',     'license',  null::text, 'available',   400, null,        40),
        (34, 'Cursor Pro',             'license',  null,       'available',   140, null,         3)
      ) as t(n, name, asset_type, serial, status, renew_days, notes, created_ago)
    loop
      v_i := rec.n;
      v_id := (
        'a55e' || lpad(v_tenant_n::text, 4, '0') || '-0000-4000-8000-' || lpad(v_i::text, 12, '0')
      )::uuid;
      v_type := rec.asset_type;
      v_status := rec.status;
      v_user := null;
      v_user_name := null;
      v_serial := rec.serial;

      if v_status = 'assigned' then
        v_user := v_user_id;
        v_user_name := v_name;
        if v_type = 'license' then
          v_serial := v_email;
        end if;
      elsif v_type = 'license' then
        v_serial := 'pool-' || v_i::text;
      end if;

      insert into sominnercore.ops_assets (
        id, tenant_id, name, asset_type, serial_or_key, status,
        assigned_user_id, assigned_user_name, renewal_date, notes,
        created_at, updated_at
      )
      values (
        v_id,
        v_tenant,
        rec.name,
        v_type,
        v_serial,
        v_status,
        v_user,
        v_user_name,
        case when rec.renew_days is null then null else current_date + rec.renew_days end,
        case
          when rec.notes = 'warranty' then 'seed:assets|warranty'
          else 'seed:assets'
        end,
        timezone('utc', now()) - (rec.created_ago || ' days')::interval,
        timezone('utc', now())
      );
    end loop;
  end loop;

  if v_tenant_n = 0 then
    raise exception 'Asset seed aborted — no enabled products found';
  end if;
end $$;
