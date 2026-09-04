-- KobNetiApi — Engineering sample data (optional; API auto-seeds on first board load)
-- Run manually if you prefer SQL-only seeding for muuqwear
-- Idempotent: skips when tasks already exist for the tenant

do $$
declare
  v_tenant text := 'muuqwear';
  v_count int;
  v_ms_holiday uuid := 'a1000001-0001-4001-8001-000000000001';
  v_ms_checkout uuid := 'a1000001-0001-4001-8001-000000000002';
  v_ms_chat uuid := 'a1000001-0001-4001-8001-000000000003';
  v_ms_returns uuid := 'a1000001-0001-4001-8001-000000000004';
begin
  select count(*) into v_count from sominnercore.eng_tasks where tenant_id = v_tenant;
  if v_count > 0 then
    raise notice 'Engineering seed skipped — % tasks already exist for %', v_count, v_tenant;
    return;
  end if;

  insert into sominnercore.eng_milestones
    (id, tenant_id, title, description, status, target_date, start_date, sort_order, created_at, updated_at)
  values
    (v_ms_holiday, v_tenant, 'MuuqWear Holiday Drop', 'Festive collection + promo banners', 'active',
     (current_date + 18), (current_date - 7), 1, now(), now()),
    (v_ms_checkout, v_tenant, 'Checkout & Payments Hardening', '3DS2 and address validation', 'active',
     (current_date + 8), (current_date - 14), 2, now(), now()),
    (v_ms_chat, v_tenant, 'Support Widget v2', 'Sticky notes + escalate to incident', 'planned',
     (current_date + 35), (current_date + 5), 3, now(), now()),
    (v_ms_returns, v_tenant, 'Returns & Size Guide', 'Self-serve returns portal', 'completed',
     (current_date - 28), (current_date - 45), 0, now(), now());

  insert into sominnercore.eng_tasks
    (tenant_id, task_number, title, task_type, status, priority, estimate_points, assignee_name, milestone_id, github_pr_url, created_at, updated_at)
  values
    (v_tenant, 'TASK-20260801-0001', 'Add Somali + Arabic size chart PDFs', 'feature', 'backlog', 'medium', 3, 'Adeel D.', v_ms_returns, null, now() - interval '2 days', now()),
    (v_tenant, 'TASK-20260802-0002', 'M-Pesa refund webhook reconciliation', 'feature', 'backlog', 'high', 5, 'Sarah K.', v_ms_checkout, null, now() - interval '1 day', now()),
    (v_tenant, 'TASK-20260803-0003', 'Wishlist share links for Instagram stories', 'feature', 'backlog', 'low', 2, null, v_ms_holiday, null, now() - interval '3 days', now()),
    (v_tenant, 'TASK-20260804-0004', 'Fix Safari address autofill on checkout', 'bug', 'ready', 'high', 2, 'Ibrahim M.', v_ms_checkout, null, now() - interval '1 day', now()),
    (v_tenant, 'TASK-20260805-0005', 'Product API: cache stock counts per variant', 'chore', 'ready', 'medium', 3, 'Sarah K.', v_ms_holiday, null, now(), now()),
    (v_tenant, 'TASK-20260806-0006', 'Live chat handoff uses Support Hub sticky notes', 'feature', 'in_progress', 'critical', 5, 'Adeel D.', v_ms_chat, null, now(), now()),
    (v_tenant, 'TASK-20260807-0007', 'CDN cache headers for lookbook images', 'chore', 'in_progress', 'medium', 2, 'Leila H.', v_ms_holiday, null, now() - interval '6 hours', now()),
    (v_tenant, 'TASK-20260808-0008', 'Stripe 3DS2 for international cards', 'feature', 'in_review', 'critical', 5, 'Ibrahim M.', v_ms_checkout,
     'https://github.com/kobneti/muuqwear-web/pull/142', now() - interval '12 hours', now()),
    (v_tenant, 'TASK-20260809-0009', 'Deploy Ramadan promo landing page', 'feature', 'done', 'high', 3, 'Adeel D.', v_ms_holiday, null, now() - interval '10 days', now() - interval '10 days'),
    (v_tenant, 'TASK-20260810-0010', 'Fix guest cart merge null reference', 'bug', 'done', 'high', 2, 'Sarah K.', v_ms_checkout, null, now() - interval '7 days', now() - interval '7 days'),
    (v_tenant, 'TASK-20260811-0011', 'Returns portal SLA dashboard tiles', 'feature', 'done', 'medium', 3, 'Leila H.', v_ms_returns, null, now() - interval '20 days', now() - interval '20 days');

  raise notice 'Engineering seed inserted for %', v_tenant;
end $$;
