-- KobNetiApi — Payroll sample data for the live KobNeti staff roster
-- Schema: sominnercore
-- Idempotent: replaces only rows tagged as this seed (notes = 'seed:payroll' / stable ids)
-- Uses the first active staff profile (prefers admin@sominnercore.com) — no invented employees.
-- Seeds KobNeti only. Customer products (muuqwear/salguri/gaarx) are cleared of the copied demo.

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

-- Remove the demo roster that was copied onto customer products.
delete from sominnercore.ops_pay_periods
where tenant_id in ('muuqwear', 'salguri', 'gaarx');

delete from sominnercore.ops_pay_rates
where tenant_id in ('muuqwear', 'salguri', 'gaarx');

delete from sominnercore.ops_approval_requests
where tenant_id in ('muuqwear', 'salguri', 'gaarx')
  and request_type = 'payroll';

delete from sominnercore.ops_time_entries
where tenant_id in ('muuqwear', 'salguri', 'gaarx')
  and notes in ('seed:payroll', 'Approved timesheet');

do $$
declare
  v_tenant text := 'kobneti';
  v_staff_id uuid;
  v_user_id uuid;
  v_name text;
  v_role text;
  v_rate numeric(12,2);
  v_cur_start date;
  v_cur_end date;
  v_prev_start date;
  v_prev_end date;
  v_cur_minutes int := 4800; -- 80.0 hrs
  v_prev_minutes int := 4560; -- 76.0 hrs
  v_cur_amount numeric(14,2);
  v_prev_amount numeric(14,2);
  v_rate_id uuid;
  v_prev_id uuid;
  v_cur_id uuid;
  v_appr_id uuid;
  v_label_cur text;
  v_label_prev text;
  v_lines_cur text;
  v_lines_prev text;
  v_day date;
  v_left int;
  v_chunk int;
begin
  select sp.id,
         coalesce(sp.user_id, sp.id),
         coalesce(nullif(btrim(sp.display_name), ''), initcap(replace(split_part(sp.email, '@', 1), '.', ' '))),
         coalesce(nullif(sp.role, ''), 'admin')
    into v_staff_id, v_user_id, v_name, v_role
  from sominnercore.staff_profiles sp
  where coalesce(sp.active, true)
    and coalesce(sp.status, 'active') = 'active'
    and sp.email not ilike '%@sominnercore.local'
  order by
    case when sp.email ilike 'admin@sominnercore.com' then 0 else 1 end,
    sp.created_at
  limit 1;

  if v_staff_id is null then
    raise exception 'Payroll seed aborted — no active staff_profiles row found';
  end if;

  v_rate := case v_role
    when 'admin' then 45.00
    when 'manager' then 42.00
    when 'engineer' then 38.00
    else 30.00
  end;
  v_cur_amount := round((v_cur_minutes / 60.0) * v_rate, 2);
  v_prev_amount := round((v_prev_minutes / 60.0) * v_rate, 2);

  if extract(day from current_date) <= 15 then
    v_cur_start := date_trunc('month', current_date)::date;
    v_cur_end := v_cur_start + 14;
    v_prev_end := (v_cur_start - 1);
    v_prev_start := date_trunc('month', v_prev_end)::date + 15;
  else
    v_cur_start := date_trunc('month', current_date)::date + 15;
    v_cur_end := (date_trunc('month', current_date) + interval '1 month - 1 day')::date;
    v_prev_start := date_trunc('month', current_date)::date;
    v_prev_end := v_prev_start + 14;
  end if;

  v_label_cur := to_char(v_cur_start, 'Mon FMDD') || ' – ' || to_char(v_cur_end, 'Mon FMDD, YYYY');
  v_label_prev := to_char(v_prev_start, 'Mon FMDD') || ' – ' || to_char(v_prev_end, 'Mon FMDD, YYYY');

  v_rate_id := md5(v_tenant || ':ops-pay-rate')::uuid;
  v_prev_id := md5(v_tenant || ':ops-pay-period-prev')::uuid;
  v_cur_id := md5(v_tenant || ':ops-pay-period-cur')::uuid;
  v_appr_id := md5(v_tenant || ':ops-pay-approval-cur')::uuid;

  delete from sominnercore.ops_time_entries
  where tenant_id = v_tenant and notes in ('seed:payroll', 'Approved timesheet');

  delete from sominnercore.ops_pay_periods
  where tenant_id = v_tenant;

  delete from sominnercore.ops_approval_requests
  where tenant_id = v_tenant and request_type = 'payroll';

  delete from sominnercore.ops_pay_rates
  where tenant_id = v_tenant;

  insert into sominnercore.ops_pay_rates
    (id, tenant_id, staff_id, user_id, role, hourly_rate, currency, effective_from, created_at)
  values
    (v_rate_id, v_tenant, v_staff_id, v_user_id, v_role, v_rate, 'USD', v_prev_start, timezone('utc', now()));

  v_left := v_prev_minutes;
  v_day := v_prev_start;
  while v_left > 0 and v_day <= v_prev_end loop
    v_chunk := least(480, v_left);
    insert into sominnercore.ops_time_entries
      (tenant_id, user_id, user_name, entry_type, clock_in, clock_out, minutes, notes, status, created_at)
    values
      (v_tenant, v_user_id, v_name, 'manual',
       (v_day + time '09:00') at time zone 'utc',
       (v_day + time '09:00' + make_interval(mins => v_chunk)) at time zone 'utc',
       v_chunk, 'seed:payroll', 'approved',
       (v_day + time '09:00') at time zone 'utc');
    v_left := v_left - v_chunk;
    v_day := v_day + 1;
  end loop;

  v_left := v_cur_minutes;
  v_day := v_cur_start;
  while v_left > 0 and v_day <= v_cur_end loop
    v_chunk := least(480, v_left);
    insert into sominnercore.ops_time_entries
      (tenant_id, user_id, user_name, entry_type, clock_in, clock_out, minutes, notes, status, created_at)
    values
      (v_tenant, v_user_id, v_name, 'manual',
       (v_day + time '09:00') at time zone 'utc',
       (v_day + time '09:00' + make_interval(mins => v_chunk)) at time zone 'utc',
       v_chunk, 'seed:payroll', 'approved',
       (v_day + time '09:00') at time zone 'utc');
    v_left := v_left - v_chunk;
    v_day := v_day + 1;
  end loop;

  v_lines_prev := json_build_array(json_build_object(
    'userId', v_user_id,
    'userName', v_name,
    'minutes', v_prev_minutes,
    'hourlyRate', v_rate,
    'amount', v_prev_amount
  ))::text;

  v_lines_cur := json_build_array(json_build_object(
    'userId', v_user_id,
    'userName', v_name,
    'minutes', v_cur_minutes,
    'hourlyRate', v_rate,
    'amount', v_cur_amount
  ))::text;

  insert into sominnercore.ops_pay_periods
    (id, tenant_id, label, starts_on, ends_on, status, total_minutes, total_amount, currency, lines_json, approval_id, created_at, updated_at)
  values
    (v_prev_id, v_tenant, v_label_prev, v_prev_start, v_prev_end, 'exported',
     v_prev_minutes, v_prev_amount, 'USD', v_lines_prev, null,
     timezone('utc', now()) - interval '20 days',
     (v_prev_end + 5)::timestamp at time zone 'utc');

  insert into sominnercore.ops_approval_requests
    (id, tenant_id, request_type, status, payload_json, requester_user_id, requester_name, created_at, updated_at)
  values
    (v_appr_id, v_tenant, 'payroll', 'pending',
     json_build_object('payPeriodId', v_cur_id, 'totalAmount', v_cur_amount)::text,
     v_user_id, v_name, timezone('utc', now()) - interval '6 hours', timezone('utc', now()) - interval '6 hours');

  insert into sominnercore.ops_pay_periods
    (id, tenant_id, label, starts_on, ends_on, status, total_minutes, total_amount, currency, lines_json, approval_id, created_at, updated_at)
  values
    (v_cur_id, v_tenant, v_label_cur, v_cur_start, v_cur_end, 'pending_approval',
     v_cur_minutes, v_cur_amount, 'USD', v_lines_cur, v_appr_id,
     timezone('utc', now()) - interval '2 days', timezone('utc', now()) - interval '6 hours');

  raise notice 'Payroll seed inserted for kobneti as % (% hrs @ %, current %)',
    v_name, v_cur_minutes / 60.0, v_rate, v_label_cur;
end $$;
