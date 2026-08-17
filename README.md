# KobNetiApi — Operations API

Multi-tenant **ops** backend for KobNeti staff (support hub first; more modules later).

**Product APIs stay separate** (e.g. MuuqWearApi). This service is the control plane.

**First product tenants:** `muuqwear`, `salguri`, `gaarx`  
**Consumers:** storefront widgets + **KobNeti** ops UI (Support Hub)

**Postgres schema:** `sominnercore` (unchanged name; do not rename).

## Projects

| Path | Role |
|------|------|
| `KobNeti.Api/` | ASP.NET Core Web API (`net9.0`) |
| `KobNeti.Api.Tests/` | Tenant isolation tests |
| `supabase/support_schema.sql` | Support tables in schema `sominnercore` |
| `supabase/products_registry.sql` | Product Registry (`products`) + seed |
| `docs/support-muuqwear-cutover.md` | MuuqWear cutover + auth headers |

## Product Registry

- `GET /api/products` — enabled products (admin JWT + `X-Tenant-Key`)
- `GET /api/Support/tenants` — same list shape the Hub already uses
- Resolution: DB `sominnercore.products` when Supabase is configured; otherwise `Support:Tenants` config
- Secrets (`JwtSecret`, optional `UpstreamApiBaseUrl`) can still override from env/config

## Run locally

```bash
dotnet run --project KobNeti.Api
```

Default URL: `http://localhost:5241/`  
Default store: **in-memory** until `Supabase:ServiceRoleKey` is set and `Support:UseInMemoryStore` is `false`.

Apply `supabase/support_schema.sql` before using the real store. Keep schema name **`sominnercore`**.

## Auth quick reference

| Call type | Headers |
|-----------|---------|
| Public (guest chat, ticket submit, published KB) | `X-Tenant-Key` |
| Agent (admin chat/tickets/KB) | `X-Tenant-Key` + `Authorization: Bearer <HS256 JWT>` |

Agent JWT claims: `sub`, `email`, `app_role` ∈ `admin` \| `manager` \| `support` (legacy `support_team`), and one or more `product` claims (`*` = all products).

Ops UI exchanges a Supabase access token via `POST /api/SupportAuth/exchange`.

Staff source of truth: `sominnercore.staff_profiles` + `staff_product_access` (SQL: `supabase/staff_access.sql`), with optional `Support:Staff` config fallback. Platform admins: `app_metadata.role=admin` or `Support:CoreAdminEmails`.

Dev tenant key for muuqwear: `pk_muuqwear_dev_public` (see `appsettings.json`).

## Tests

```bash
dotnet test
```

## Related

- KobNeti ops UI (WASM): sibling `KobNeti` (formerly Sominnercore)
- MuuqWear API (product spoke): `MuuqWearApi`
