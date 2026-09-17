-- Internal Chat: channel topic + threaded replies
-- Apply on sominnercore after support_w7_insights.sql

alter table sominnercore.ops_im_channels
  add column if not exists topic text not null default '';

alter table sominnercore.ops_im_messages
  add column if not exists parent_message_id uuid null
    references sominnercore.ops_im_messages(id) on delete cascade;

create index if not exists ix_ops_im_messages_parent
  on sominnercore.ops_im_messages (channel_id, parent_message_id);
