# Widget embed snippet (W1.13)

Each product has a unique **public key** (`products.public_key` / `X-Tenant-Key`).  
Use that key in the storefront to identify the product for public Support APIs (chat send, ticket form, help articles).

## Install snippet

```html
<script
  src="https://YOUR-KOBNETI-API.onrender.com/widget/support.js"
  data-tenant-key="pk_your_product_public_key"
  data-api-base="https://YOUR-KOBNETI-API.onrender.com"
  async></script>
```

Until the hosted `support.js` widget ships (W2), call the public APIs directly with header:

```http
X-Tenant-Key: pk_your_product_public_key
```

## Rotate key

Platform admins can rotate a product key:

```http
POST /api/products/{slug}/rotate-key
Authorization: Bearer {agent-jwt}
X-Tenant-Key: {any-valid-current-key}
```

Response includes the new `publicKey` and a fresh `widgetSnippet`.  
Update the storefront immediately after rotation; the old key stops resolving.

Fetch the current snippet without rotating:

```http
GET /api/products/{slug}/widget-snippet
```
