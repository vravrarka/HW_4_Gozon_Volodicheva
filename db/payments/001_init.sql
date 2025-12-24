create table if not exists accounts (
  user_id uuid primary key,
  balance numeric(18,2) not null default 0 check (balance >= 0),
  version int not null default 0,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists inbox_messages (
  id uuid primary key, 
  received_at timestamptz not null,
  event_type text not null
);

create table if not exists payment_transactions (
  order_id uuid primary key, 
  user_id uuid not null,
  amount numeric(18,2) not null check (amount > 0),
  result text not null check (result in ('Succeeded','Failed')),
  created_at timestamptz not null
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

create index if not exists ix_pay_outbox_unpublished
on outbox_messages (published_at)
where published_at is null;
