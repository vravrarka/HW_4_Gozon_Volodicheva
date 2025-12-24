create table if not exists orders (
  id uuid primary key,
  user_id uuid not null,
  amount numeric(18,2) not null check (amount > 0),
  status text not null check (status in ('NEW','FINISHED','CANCELLED')),
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists outbox_messages (
  id uuid primary key, 
  occurred_at timestamptz not null,
  event_type text not null,
  exchange text not null,
  routing_key text not null,
  payload jsonb not null,
  published_at timestamptz null,
  attempts int not null default 0,
  last_error text null
);

create index if not exists ix_orders_outbox_unpublished
on outbox_messages (published_at)
where published_at is null;

create table if not exists inbox_messages (
  id uuid primary key, 
  received_at timestamptz not null,
  event_type text not null
);
