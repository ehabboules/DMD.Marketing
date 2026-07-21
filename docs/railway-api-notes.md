# Railway GraphQL API — PostgreSQL Provisioning Notes

## Endpoint & Authentication

```
POST https://backboard.railway.com/graphql/v2
Authorization: Bearer <ACCOUNT_OR_WORKSPACE_TOKEN>
Content-Type: application/json
```

| Token type | Header | Scope |
|---|---|---|
| Account token | `Authorization: Bearer` | All resources |
| Workspace token | `Authorization: Bearer` | One workspace |
| Project token | `Project-Access-Token` | One environment in one project |

Use an **Account token** for provisioning (needs cross-resource access).

---

## Complete Provisioning Sequence

Order matters — follow these steps in sequence:

1. `serviceCreate` → get `serviceId`
2. `volumeCreate` → attach persistent storage
3. `tcpProxyCreate` → get `domain` + `proxyPort` for external access
4. `serviceInstanceDeployV2` → trigger first deployment
5. Poll `deployments` until `status == "SUCCESS"`
6. `variables` query → read `DATABASE_URL`, `PGPASSWORD`, etc.

---

## 1. Create PostgreSQL Service

Railway's managed Postgres image: `ghcr.io/railwayapp-templates/postgres-ssl:edge`

```graphql
mutation serviceCreate($input: ServiceCreateInput!) {
  serviceCreate(input: $input) {
    id
    name
    projectId
    createdAt
  }
}
```

**Variables:**
```json
{
  "input": {
    "projectId": "YOUR_PROJECT_ID",
    "name": "Postgres",
    "source": {
      "image": "ghcr.io/railwayapp-templates/postgres-ssl:edge"
    }
  }
}
```

**Response:**
```json
{
  "data": {
    "serviceCreate": {
      "id": "srv_abc123",
      "name": "Postgres",
      "projectId": "proj_xyz",
      "createdAt": "2026-07-19T10:00:00Z"
    }
  }
}
```

> ⚠️ **Quirk:** `source` only accepts `{ image: "..." }` or `{ repo: "owner/repo" }`.
> Do NOT add a `source.type` field — it will cause `"Problem processing request"` errors.

---

## 2. Attach Persistent Volume

Without a volume, data is lost when the container restarts.

```graphql
mutation volumeCreate($input: VolumeCreateInput!) {
  volumeCreate(input: $input) {
    id
  }
}
```

**Variables:**
```json
{
  "input": {
    "projectId": "YOUR_PROJECT_ID",
    "serviceId": "srv_abc123",
    "environmentId": "YOUR_ENV_ID",
    "mountPath": "/var/lib/postgresql/data"
  }
}
```

---

## 3. Create TCP Proxy (External Access)

By default Postgres is only reachable inside the Railway private network.
This creates a public hostname + port.

```graphql
mutation tcpProxyCreate($input: TcpProxyCreateInput!) {
  tcpProxyCreate(input: $input) {
    id
    domain
    proxyPort
    applicationPort
  }
}
```

**Variables:**
```json
{
  "input": {
    "environmentId": "YOUR_ENV_ID",
    "serviceId": "srv_abc123",
    "applicationPort": 5432
  }
}
```

**Response:**
```json
{
  "data": {
    "tcpProxyCreate": {
      "id": "proxy_abc",
      "domain": "roundhouse.proxy.rlwy.net",
      "proxyPort": 55432,
      "applicationPort": 5432
    }
  }
}
```

The external connection string will be:
```
postgresql://postgres:<PGPASSWORD>@roundhouse.proxy.rlwy.net:55432/railway
```

**List existing TCP proxies:**
```graphql
query tcpProxies($serviceId: String!, $environmentId: String!) {
  tcpProxies(serviceId: $serviceId, environmentId: $environmentId) {
    id
    domain
    proxyPort
    applicationPort
  }
}
```

---

## 4. Trigger Deployment

```graphql
mutation serviceInstanceDeployV2($serviceId: String!, $environmentId: String!) {
  serviceInstanceDeployV2(serviceId: $serviceId, environmentId: $environmentId)
}
```

**Variables:**
```json
{
  "serviceId": "srv_abc123",
  "environmentId": "YOUR_ENV_ID"
}
```

---

## 5. Poll for Deployment Ready

Poll every 3–5 seconds. Postgres typically reaches `SUCCESS` in 30–90 seconds.
Timeout after 5 minutes.

```graphql
query deployments($input: DeploymentListInput!, $first: Int) {
  deployments(input: $input, first: $first) {
    edges {
      node {
        id
        status
        createdAt
      }
    }
  }
}
```

**Variables:**
```json
{
  "input": {
    "projectId": "YOUR_PROJECT_ID",
    "serviceId": "srv_abc123",
    "environmentId": "YOUR_ENV_ID"
  },
  "first": 1
}
```

**DeploymentStatus enum:**

| Status | Meaning |
|---|---|
| `INITIALIZING` | Accepted, waiting in queue |
| `QUEUED` | In build queue |
| `BUILDING` | Image building |
| `DEPLOYING` | Container starting |
| `SUCCESS` | ✅ Running and healthy |
| `FAILED` | ❌ Terminal failure |
| `CRASHED` | ❌ Started but crashed |
| `SLEEPING` | Scaled to zero |
| `REMOVED` | Deployment removed |

**Success condition:** `status == "SUCCESS"`
**Failure conditions:** `status == "FAILED"` or `status == "CRASHED"`

---

## 6. Read Environment Variables (Connection String)

Call **after** deployment reaches `SUCCESS`. Variables are `null` before then.

```graphql
query variables($projectId: String!, $environmentId: String!, $serviceId: String) {
  variables(
    projectId: $projectId
    environmentId: $environmentId
    serviceId: $serviceId
  )
}
```

**Variables:**
```json
{
  "projectId": "YOUR_PROJECT_ID",
  "environmentId": "YOUR_ENV_ID",
  "serviceId": "srv_abc123"
}
```

**Response** (returns a raw JSON scalar — deserialize as `Dictionary<string, string>`):
```json
{
  "data": {
    "variables": {
      "DATABASE_URL": "postgresql://postgres:secretpassword@roundhouse.proxy.rlwy.net:55432/railway",
      "PGHOST": "roundhouse.proxy.rlwy.net",
      "PGPORT": "5432",
      "PGUSER": "postgres",
      "PGPASSWORD": "secretpassword",
      "PGDATABASE": "railway",
      "PGDATA": "/var/lib/postgresql/data"
    }
  }
}
```

> ⚠️ Omit `unrendered` or pass `false` to get resolved values.
> Passing `unrendered: true` returns reference tokens like `${{Postgres.DATABASE_URL}}` instead.

---

## 7. Inject Variables Into Another Service (Optional)

If StockShopOnline needs the connection string injected as an env var:

```graphql
mutation variableCollectionUpsert($input: VariableCollectionUpsertInput!) {
  variableCollectionUpsert(input: $input)
}
```

**Variables:**
```json
{
  "input": {
    "projectId": "YOUR_PROJECT_ID",
    "environmentId": "YOUR_ENV_ID",
    "serviceId": "STOCKSHOPONLINE_SERVICE_ID",
    "variables": {
      "TENANT_SLUG_CONNECTION_STRING": "postgresql://..."
    },
    "replace": false,
    "skipDeploys": false
  }
}
```

---

## Rate Limits

| Plan | Requests/Hour | Requests/Second |
|---|---|---|
| Free | 100 | — |
| Hobby | 1,000 | 10 |
| Pro | 10,000 | 50 |
| Enterprise | Custom | Custom |

**Rate limit response headers:**
- `X-RateLimit-Limit` — daily maximum
- `X-RateLimit-Remaining` — requests remaining
- `X-RateLimit-Reset` — window reset timestamp
- `Retry-After` — seconds to wait on HTTP 429

---

## Common Errors

| Error | Cause | Fix |
|---|---|---|
| `"Problem processing request"` | `source.type` field in `ServiceCreateInput` | Remove `source.type` — use only `source.image` |
| `"Unauthenticated"` | Missing or expired token | Add `Authorization: Bearer <TOKEN>` |
| `"Not found"` | Wrong projectId/envId or insufficient token scope | Use account token, verify IDs in Railway dashboard |
| HTTP 429 | Rate limit exceeded | Honor `Retry-After` header |
| `variables` returns `null` for DB vars | Deployment not yet `SUCCESS` | Wait for deployment to complete before reading |
| `tcpProxyCreate` fails | Port already proxied | Query `tcpProxies` first — only one proxy per port per service |

---

## C# Deserialization Notes

- `variables` query returns a raw GraphQL scalar (JSON object), not a typed list.
- Deserialize as `Dictionary<string, string>` using `System.Text.Json`.
- All Railway IDs are `String` (not int/GUID), even though they look like UUIDs.
- No webhook for deployment completion via public API — must poll.

---

## Sources

- https://docs.railway.com/integrations/api
- https://docs.railway.com/integrations/api/api-cookbook
- https://docs.railway.com/integrations/api/manage-services
- https://docs.railway.com/integrations/api/manage-variables
- https://docs.railway.com/integrations/api/manage-deployments
- https://docs.railway.com/networking/tcp-proxy
- https://docs.railway.com/databases/postgresql
- GraphiQL playground: https://railway.com/graphiql
