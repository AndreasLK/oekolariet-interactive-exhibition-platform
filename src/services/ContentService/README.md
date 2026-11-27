# ContentService

**ContentService** is the media, exhibition template, and offline publish-bundle service for the Økolariet Interactive Exhibition Platform.

It is built with **Clean Architecture** and supports both single-node local development and multi-node horizontal storage pooling using **Dapr** on an internal Docker network.

---

## 1. Overall Idea & Architecture

### Layer Structure

- **`ContentService.Domain`** — Core entities (`ImageEntity`, `ImageBinaryEntity`, `TemplateEntity`), value objects (`PictureReference`), enums (`TemplateComponentType`, `StorageHealthStatus`), and domain exceptions.
- **`ContentService.Application`** — Use cases, storage quota monitoring, magic-byte image validation, and repository/storage abstractions.
- **`ContentService.Facade`** — CQS facade interfaces and DTO boundary separating internal use cases from HTTP endpoints.
- **`ContentService.Infrastructure`** — SQL Server + EF Core persistence, streaming image binary storage, and Dapr scatter-gather / node-forwarding adapters.
- **`ContentService.Api`** — The main production host: versioned ASP.NET Core Minimal API (`/api/v1/...`), RFC 7807 `GlobalExceptionHandler`, CORS, OpenAPI/Swagger, and Dapr Pub/Sub subscription endpoints.
- **`ContentService.Sdk`** — Standalone Refit-backed `.NET` client library for external consumers (such as `EditorService` and Blazor WebAssembly apps) with zero dependency on `Domain`, `Facade`, or Dapr.
- **`ContentService.Components`** — Reusable pure C#/Razor Blazor UI components (image gallery, upload modal, metadata editor) consumed by host UIs such as `EditorService`.
- **`ContentService.Web`** — **Dev/showcase host only** (used locally to preview and exercise `ContentService.Components` and `ContentService.Sdk`). It is **not** deployed or run in production—`ContentService.Api` is the sole runtime entry point in production.

### Multi-Node Sharded Storage Pool

Each `ContentService` instance owns a dedicated SQL Server database and is identified by `ContentStorage:StorageNodeId` (for example `node-1`, `node-2`):

- Images receive a **shard-aware identifier** (`ExternalId = "{node}_{guid}"`) and a **schema reference** (`PictureReference = "picture.{node}.{guid}"`).
- If a client requests, updates, streams, or deletes `{node}_{guid}` on *any* node in the pool, that node checks whether it owns `{node}`. If another node owns it, the receiving node transparently forwards the request over the internal Dapr sidecar (`X-Node-Forwarded: true` prevents forwarding loops).
- When uploading or locating assets without a node hint, nodes discover peers dynamically over **Dapr Pub/Sub** (`contentservice-pubsub` backed by Redis) instead of maintaining hardcoded peer lists.

---

## 2. Communication Model

```text
  EditorService / Blazor WASM
              │
              │  HTTP/HTTPS (/api/v1/...) via ContentService.Sdk
              ▼
   ┌─────────────────────┐          Internal Docker Network          ┌─────────────────────┐
   │ contentservice-node-1│ ◄── Dapr Sidecar (:3500) + Redis ──────► │ contentservice-node-2│
   │   + SQL Server 1    │      (Pub/Sub & Service Invocation)       │   + SQL Server 2    │
   └─────────────────────┘                                           └─────────────────────┘
```

### External Consumers (`EditorService`, Blazor WebAssembly, Kiosks)

- External consumers talk **only** to `ContentService.Api` over standard HTTP/HTTPS (`/api/v1/...`) using **`ContentService.Sdk`**.
- Consumers do **not** run Dapr sidecars to talk to `ContentService`, and do **not** need Dapr tokens.
- In Docker Compose, point consumer `ContentService:BaseAddress` to the service container URL (e.g., `http://contentservice-node-1:8080`) or reverse-proxy URL (e.g., `https://content.example.com/content` — subpaths are preserved automatically).

### Internal Docker & Dapr Network (Node-to-Node)

- Each `ContentService` node pairs with a `daprd` sidecar sharing its network namespace (`network_mode: "service:contentservice-node-1"`).
- Nodes talk to their local sidecar on `http://localhost:3500` (`DAPR_HTTP_PORT`, default `3500`) to:
  1. **Invoke peer nodes** via `/v1.0/invoke/contentservice-{nodeId}/method/...` (with `ServiceInvocationStreaming` enabled in [dapr/config.yaml](dapr/config.yaml)).
  2. **Broadcast scatter-gather queries** over Redis Pub/Sub (`contentservice-pubsub` in [dapr/components/pubsub.yaml](dapr/components/pubsub.yaml)) on topics `content.pool.locate-image` and `content.pool.upload-offer`.
- Because all Dapr sidecar traffic and node-to-node API calls stay inside the private Docker network, **no Dapr API tokens (`DAPR_API_TOKEN` / `APP_API_TOKEN`) are configured or required**.
  - *Note:* If you later expose nodes across untrusted networks and turn on Dapr token authentication, add `DAPR_API_TOKEN` / `APP_API_TOKEN` to `.env` and [docker-compose.yml](docker-compose.yml), attach `dapr-api-token` in `DaprNodeRequestSender`, and validate it in `ContentService.Api`.

---

## 3. Setting Up & Running ContentService

### Option A: Multi-Node Docker Compose Pool (Recommended)

From `src/services/ContentService/`:

```bash
cp .env.example .env
docker compose up --build
```

This starts:
- `contentservice-node-1` (`http://localhost:5101`) + `contentservice-node-1-dapr` + `sql-node-1` (`localhost:14331`)
- `contentservice-node-2` (`http://localhost:5102`) + `contentservice-node-2-dapr` + `sql-node-2` (`localhost:14332`)
- `redis` (`localhost:6379`) and `dapr-placement` (`localhost:50006`)

All ports, SQL passwords, storage quotas, pool timeouts, and CORS origins in `docker-compose.yml` read from `.env` (see [`.env.example`](.env.example)) with safe local-dev defaults if `.env` is omitted. Note that `.env` is ignored by Git (`.gitignore`), while `.env.example` is tracked.

### Option B: Standalone Local API (Single Node)

From `src/services/ContentService/`:

```bash
dotnet run --project ContentService.Api/ContentService.Api.csproj
```

Key configuration keys in [ContentService.Api/appsettings.json](ContentService.Api/appsettings.json) (or environment variables using `__`):

| Setting | Purpose | Default |
| :--- | :--- | :--- |
| `ConnectionStrings:ContentDatabase` | SQL Server connection string for metadata & image binaries | LocalDB `OekolarietContentService` |
| `Database:AutoMigrateOnStartup` | Runs EF Core migrations automatically on startup (set `"true"` in containers) | `false` / unset |
| `ContentStorage:StorageNodeId` | Unique node identifier stamped into `ExternalId` and `PictureReference` | `"node-1"` |
| `ContentStorage:PublicBaseUrl` | Public HTTP base URL advertised in pool upload offers | `null` |
| `ContentStorage:PubSubName` | Dapr Pub/Sub component name | `"contentservice-pubsub"` |
| `ContentStorage:PoolRequestTimeoutSeconds` | Scatter-gather timeout when querying peer nodes | `5` |
| `ContentStorage:MaxImageSizeBytes` | Maximum accepted upload payload size | `10485760` (10 MiB) |
| `ContentStorage:TotalQuotaBytes` | Node storage quota limit | `5368709120` (5 GiB) |
| `Cors:AllowedOrigins` | Allowed origins for Blazor WebAssembly clients | `[]` |

---

## 4. Consuming ContentService with `ContentService.Sdk`

Add a project reference to `ContentService.Sdk` and configure the `ContentService` section in `appsettings.json`:

```json
{
  "ContentService": {
    "BaseAddress": "http://contentservice-node-1:8080",
    "ApiVersion": "v1",
    "DefaultActorId": "editor-service",
    "RequestTimeout": "00:00:30"
  }
}
```

Register the SDK in `Program.cs`:

```csharp
builder.Services.AddContentServiceSdk(builder.Configuration);
// Or: builder.Services.AddContentServiceClient(builder.Configuration);
```

Options are validated on startup (`ContentServiceClientOptionsValidator`):
- `BaseAddress` must be an absolute `http` or `https` URI.
- `ApiVersion` must match `^v\d+$` (defaults to `v1`).
- `DefaultActorId`, when provided, must contain printable 7-bit ASCII characters (sent via `X-Actor-Id` on mutating requests).
- `RequestTimeout`, when provided, must be greater than `TimeSpan.Zero`.

### Available SDK Clients & Contracts

All contracts live in `ContentService.Sdk.Models` and follow **`*Command`**, **`*Query`**, and **`*Response`** naming:

1. **`IContentServiceImageClient`**
   - `UploadImageAsync(ContentUploadImageCommand)` → `ContentImageResponse`
   - `ListImagesAsync(ContentImageQuery)` / `(page, pageSize, searchTerm)` → `ContentPagedResponse<ContentImageResponse>`
   - `ListAlbumsAsync(parentPath)` → `IReadOnlyList<string>`
   - `GetImageMetadataAsync(Guid | string imageIdentifier)` → `ContentImageResponse`
   - `GetImagesMetadataBatchAsync(IReadOnlyCollection<Guid>)` → `IReadOnlyList<ContentImageResponse>`
   - `UpdateImageMetadataAsync(Guid | string imageIdentifier, ContentUpdateImageMetadataCommand)` → `ContentImageResponse`
   - `DownloadImageAsync(Guid | string imageIdentifier)` → `ContentImageDownloadResponse` *(caller must `await using` / dispose)*
   - `DownloadImagePreviewAsync(Guid | string imageIdentifier)` → `ContentImageDownloadResponse` *(lightweight preview stream)*
   - `DeleteImageAsync(Guid | string imageIdentifier, actorId)`
   - `GetImageContentUri(Guid | string imageIdentifier)` / `GetImagePreviewUri(Guid | string imageIdentifier)` *(for `<img src>` rendering)*

2. **`IContentServiceTemplateClient`**
   - `CreateTemplateAsync(ContentCreateTemplateCommand)` → `ContentTemplateResponse`
   - `ListTemplatesAsync(ContentTemplateQuery)` / `(page, pageSize, componentType, searchTerm)` → `ContentPagedResponse<ContentTemplateResponse>`
   - `GetTemplateAsync(Guid templateId)` → `ContentTemplateResponse`
   - `UpdateTemplateAsync(Guid templateId, ContentUpdateTemplateCommand)` → `ContentTemplateResponse`
   - `DeleteTemplateAsync(Guid templateId, actorId)`
   - `IsImageReferencedByTemplateAsync(Guid imageId)` → `ContentImageTemplateReferenceResponse`

3. **`IContentServicePlatformClient`**
   - `ResolvePublishBundleAsync(ContentResolvePublishBundleQuery)` → `ContentPublishBundleResponse`
   - `GetStorageHealthAsync()` → `ContentStorageHealthResponse`
   - `RequestUploadOfferAsync(ContentRequestUploadOfferCommand | long requiredBytes)` → `ContentPoolNodeOfferResponse`

### Error Handling & Concurrency

- Non-2xx responses throw **`ContentServiceApiException`** containing `StatusCode`, bounded `RawResponseBody` (capped at 16 KiB), and parsed RFC 7807 **`ContentServiceProblemDetails`** when available.
- Image metadata updates and template updates require `ExpectedConcurrencyStamp` (from the latest `ContentImageResponse.ConcurrencyStamp` or `ContentTemplateResponse.ConcurrencyStamp`) and return HTTP `409 Conflict` on stale edits.
- Deleting an image still referenced by an active template returns HTTP `409 Conflict`.

---

## 5. HTTP Endpoint Overview (`/api/v1`)

| Area | Method & Route | Purpose |
| :--- | :--- | :--- |
| **Images** | `POST /api/v1/images` | Upload multipart image (`file`, optional `altText`, `albumPath`, `tags`) |
| | `GET /api/v1/images` | Paginated image catalog (`page`, `pageSize`, `searchTerm`, `albumPath`, `includeSubalbums`) |
| | `GET /api/v1/images/albums` | List immediate child album paths (`parentPath`) |
| | `POST /api/v1/images/batch-metadata` | Batch metadata lookup for a list of image `Guid`s |
| | `GET /api/v1/images/{imageIdentifier}` | Fetch metadata (`Guid` or `{node}_{guid}`) |
| | `PATCH /api/v1/images/{imageIdentifier}/metadata` | Update `altText`, `tags`, `albumPath` with optimistic concurrency |
| | `GET /api/v1/images/{imageIdentifier}/content` | Stream full image binary |
| | `GET /api/v1/images/{imageIdentifier}/preview` | Stream lightweight gallery preview binary |
| | `DELETE /api/v1/images/{imageIdentifier}` | Soft-delete unreferenced image |
| **Templates** | `POST /api/v1/templates` | Create exhibition template |
| | `GET /api/v1/templates` | Paginated template listing (`page`, `pageSize`, `componentType`, `searchTerm`) |
| | `GET /api/v1/templates/{id:guid}` | Get template metadata & JSON schema |
| | `PUT /api/v1/templates/{id:guid}` | Update template with optimistic concurrency |
| | `DELETE /api/v1/templates/{id:guid}` | Archive template |
| | `GET /api/v1/templates/image-references/{id:guid}` | Check if any active template references an image |
| **Publishing** | `POST /api/v1/publish-bundles/resolve` | Verify referenced assets and compute SHA-256 publish bundle |
| **Health** | `GET /api/v1/health/storage` | Storage quota usage and `Healthy` / `Warning` / `FullReadOnly` status |
| | `GET /healthz` | Process liveness/readiness probe |
| **Pool / Dapr** | `POST /api/v1/pool/upload-offers` | Request a healthy storage node offer for a pending upload |
| | `GET /dapr/subscribe` | Dapr Pub/Sub subscription discovery (internal) |

