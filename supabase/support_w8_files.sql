-- File management: public vs restricted access
-- Apply on sominnercore after support_w6_platform_glue.sql

alter table sominnercore.ops_files
  add column if not exists access text not null default 'restricted';

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'ops_files_access_check'
  ) then
    alter table sominnercore.ops_files
      add constraint ops_files_access_check
      check (access in ('public', 'restricted'));
  end if;
end $$;
