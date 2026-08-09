# MuuqWear → Som Inner Core Support API cutover

This document lives in the **standalone** Support API repo (`Sominnercore-SupportApi`).  
The Som Inner Core WASM app only consumes this API over HTTP (`SupportApi:BaseUrl`).

## What moved

Som Inner Core Support is a multi-tenant Support API (`Sominnercore.SupportApi`) with MuuqWear Chat/Help route and DTO parity:

| Area | Routes |
|------|--------|
| Chat | `POST api/Chat/send`, `GET api/Chat/messages/{sessionId}`, `GET api/Chat/active-sessions`, `POST api/Chat/close/{sessionId}`, `GET api/Chat/session/{sessionId}/status`, `GET api/Chat/session/{sessionId}` |
| Help | `POST api/Help/ticket`, `GET api/Help/articles`, `GET api/Help/articles/{id}`, plus `api/Help/admin/*` tickets/articles/upload/engagement |
| Core extras | `GET api/Support/counts`, `api/Support/macros`, `POST api/SupportAuth/exchange` |

Data lives in Supabase schema **`sominnercore`** (see `supabase/support_schema.sql`). Apply that SQL before switching `UseInMemoryStore` off.

## Headers (required)

| Header | Who | Purpose |
|--------|-----|---------|
| `X-Tenant-Key` | All Chat/Help/Support calls | Public tenant key → resolves `tenant_id` (muuqwear first) |
| `Authorization: Bearer <jwt>` | Agent/admin routes | HS256 agent JWT (see below) |

Public guest endpoints (chat send/messages/status, ticket submit, published KB) need only `X-Tenant-Key`.

## Agent JWT expectation (MuuqWear-compatible)

- Algorithm: **HS256**
- Claims: `sub` (user id), `email`, **`app_role`** ∈ `admin` \| `support_team`
- Policy: `AdminSupport` = admin OR support_team
- **Do not** send MuuqWear cookies to Core

Configure Core:

```json
"Support": {
  "Tenants": {
    "muuqwear": {
      "PublicKey": "pk_muuqwear_dev_public",
      "JwtSecret": "<same value as MuuqWear Authentication:JwtSecret>",
      "Enabled": true
    }
  }
}
```

If `JwtSecret` matches MuuqWear’s existing issuer secret, MuuqWear agents can keep minting tokens locally and only change the API base URL + add `X-Tenant-Key`.

## MuuqWear.Web changes (minimal)

1. Point `ApiBaseUrl` at the Support API (local example: `http://localhost:5241/`).
2. On `IChatService` / `IHelpCenterService` HttpClients, add default header:

   `X-Tenant-Key: <muuqwear PublicKey>`

3. Keep existing Bearer attachment from `AuthenticatedHttpHandler`.
4. Feature-flag friendly: keep MuuqWearApi as fallback until Support API is validated in staging.

Orders / customers / badges outside chat+tickets stay on MuuqWearApi (`api/AdminBadge/counts` is **not** fully moved; Core exposes `api/Support/counts` for `{ activeChats, openTickets }` only).

## Core Support Hub auth

1. Admin signs into Som Inner Core (Supabase Gotrue) as today.
2. Hub calls `POST /api/SupportAuth/exchange` with `{ "accessToken": "<supabase access token>" }` (no tenant header required on this route).
3. API verifies Core admin (`app_metadata.role=admin` / `is_admin`, or `Support:CoreAdminEmails`).
4. API returns a short-lived agent JWT signed with `Support:CoreAgentJwtSecret`.
5. Hub calls Support routes with that Bearer + `X-Tenant-Key`.

## Local run

```bash
# Terminal 1 — Support API (in-memory store by default until ServiceRoleKey is set)
dotnet run --project Sominnercore.SupportApi

# Terminal 2 — Blazor WASM admin
dotnet run --project SominnercoreNew.csproj
```

WASM `wwwroot/appsettings.json`:

```json
"SupportApi": {
  "BaseUrl": "http://localhost:5241/",
  "TenantId": "muuqwear",
  "TenantKey": "pk_muuqwear_dev_public"
}
```

Production: set `Supabase:ServiceRoleKey`, set `Support:UseInMemoryStore` to `false`, apply `supabase/support_schema.sql`, expose schema `sominnercore` for PostgREST.

## Data migration notes (MuuqWear → Core)

Source schema: `MuuqWear`. Target: `sominnercore`. Set `tenant_id = 'muuqwear'` on every row.

Suggested mapping:

| Source | Target |
|--------|--------|
| `chat_sessions` | `support_chat_sessions` (`user_id` → `external_customer_id`) |
| `chat_messages` | `support_chat_messages` |
| `support_tickets` | `support_tickets` |
| `support_ticket_replies` | `support_ticket_replies` |
| `help_articles` (+ steps/comments/votes) | `support_kb_*` |

Preserve Guids where possible so deep links survive. Re-check ticket_number uniqueness per tenant after import.

Example sketch:

```sql
insert into sominnercore.support_chat_sessions
  (id, tenant_id, external_customer_id, guest_name, guest_email, status, created_at, updated_at, closed_at)
select id, 'muuqwear', user_id, guest_name, guest_email, status, created_at, updated_at, closed_at
from "MuuqWear".chat_sessions;
-- repeat for messages / tickets / KB with tenant_id = 'muuqwear'
```

## Tenant stubs

- `muuqwear` — enabled  
- `salguri` / `gaarx` — config stubs (`Enabled: false`) until cutover  

## Isolation

Automated tests in `Sominnercore.SupportApi.Tests` assert tenant A cannot read tenant B’s chat sessions, tickets, or published KB articles.
