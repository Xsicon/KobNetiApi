# Widget embed & public Support APIs (W1.13 / W2.2 / W2.11)

Each product has a unique **public key** (`products.public_key` / `X-Tenant-Key`).

## Install snippet

```html
<script
  src="https://YOUR-KOBNETI-API.onrender.com/widget/support.js"
  data-tenant-key="pk_your_product_public_key"
  data-api-base="https://YOUR-KOBNETI-API.onrender.com"
  data-mode="ticket"
  data-account-id=""
  async></script>
```

- `data-mode`: `ticket` (default) shows a floating ticket form.
- `data-account-id`: optional storefront account id (W2.3).
- The widget always sends `pageUrl` = `window.location.href`.

## Public API contract (header on every call)

```http
X-Tenant-Key: pk_your_product_public_key
```

| Action | Method | Path | Body (JSON) |
|--------|--------|------|-------------|
| Submit ticket | `POST` | `/api/Help/ticket` | `name`, `email`, `category`, `subject`, `message`, optional `pageUrl`, `accountId` |
| Chat send | `POST` | `/api/Chat/send` | `message`, `guestName`, `guestEmail`, optional `sessionId` |
| Chat messages | `GET` | `/api/Chat/messages/{sessionId}` | — |
| Chat status | `GET` | `/api/Chat/session/{sessionId}/status` | — |
| Published KB | `GET` | `/api/Help/articles` | — |

Email-to-ticket: `POST /api/Help/email-to-ticket` returns **501** (W2.4 stub).

## Rotate key

```http
POST /api/products/{slug}/rotate-key
Authorization: Bearer {agent-jwt}
X-Tenant-Key: {any-valid-current-key}
```

```http
GET /api/products/{slug}/widget-snippet
```

Update the storefront immediately after rotation.
