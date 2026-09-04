-- KobNeti ops file storage (Platform → File Management)
-- Run once in Supabase SQL editor (project that hosts sominnercore schema).

insert into storage.buckets (id, name, public, file_size_limit)
values ('ops-files', 'ops-files', true, 26214400)  -- 25 MB
on conflict (id) do update
set public = excluded.public,
    file_size_limit = excluded.file_size_limit;

-- Public read for download links (service_role uploads via API; no anon write).
drop policy if exists "ops_files_public_read" on storage.objects;
create policy "ops_files_public_read"
  on storage.objects for select
  to public
  using (bucket_id = 'ops-files');
