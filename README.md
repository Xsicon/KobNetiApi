# Som Inner Core — Support API

Standalone multi-tenant Customer Support API for Murabac storefronts.

**First tenant:** `muuqwear`  
**Stubs:** `salguri`, `gaarx`  
**Consumers:** MuuqWear.Web (Chat/Help cutover) and Som Inner Core Support Hub

## Projects

| Path | Role |
|------|------|
| `Sominnercore.SupportApi/` | ASP.NET Core Web API (`net9.0`) |
| `Sominnercore.SupportApi.Tests/` | Tenant isolation tests |
| `supabase/support_schema.sql` | Postgres tables in schema `sominnercore` |
| `docs/support-muuqwear-cutover.md` | MuuqWear cutover + auth headers |

## Run locally

```bash
dotnet run --project Sominnercore.SupportApi
```

Default URL: `http://localhost:5241/`  
Default store: **in-memory** until `Supabase:ServiceRoleKey` is set and `Support:UseInMemoryStore` is `false`.

Apply `supabase/support_schema.sql` in the Supabase SQL editor before using the real store.

## Auth quick reference

| Call type | Headers |
|-----------|---------|
| Public (guest chat, ticket submit, published KB) | `X-Tenant-Key` |
| Agent (admin chat/tickets/KB) | `X-Tenant-Key` + `Authorization: Bearer <HS256 JWT>` |

Agent JWT claims (MuuqWear-compatible): `sub`, `email`, `app_role` ∈ `admin` \| `support_team`.

Core Support Hub exchanges a Supabase access token via `POST /api/SupportAuth/exchange`.

Dev tenant key for muuqwear: `pk_muuqwear_dev_public` (see `appsettings.json`).

## Tests

```bash
dotnet test
```

## Related repos

- Som Inner Core (WASM admin / Support Hub client): sibling `Sominnercore`
- MuuqWear API (legacy Chat/Help host until cutover): `MuuqWearApi`
