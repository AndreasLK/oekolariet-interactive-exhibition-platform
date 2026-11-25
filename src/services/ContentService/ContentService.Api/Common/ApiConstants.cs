namespace ContentService.Api.Common;

/// <summary>
/// Centralizes HTTP transport, caching, routing, and limit constants so endpoints, CORS setup, and error handling stay consistent across <c>ContentService.Api</c>.
/// </summary>
internal static class ApiConstants
{
        /// <summary>
        /// Configuration path used to whitelist exhibition Blazor WebAssembly origins per environment without recompiling.
        /// </summary>
        public const string CORS_ALLOWED_ORIGINS_SECTION = "Cors:AllowedOrigins";

        /// <summary>
        /// Shared policy key ensuring <c>UseCors</c> and <see cref="ExceptionHandling.GlobalExceptionHandler"/> evaluate the exact same CORS rules.
        /// </summary>
        public const string BLAZOR_CORS_POLICY_NAME = "BlazorClientPolicy";

        /// <summary>
        /// Request header allowing unauthenticated internal editor clients to supply caller audit attribution before full auth is active.
        /// </summary>
        public const string ACTOR_HEADER_NAME = "X-Actor-Id";

        /// <summary>
        /// Fallback audit actor ensuring creation and modification records remain traceable when unauthenticated callers omit <see cref="ACTOR_HEADER_NAME"/>.
        /// </summary>
        public const string DEFAULT_ACTOR_ID = "api-client";

        /// <summary>
        /// Binary kibibyte factor kept private so multipart overhead math avoids unexplained raw byte multipliers.
        /// </summary>
        private const long BYTES_PER_KIBIBYTE = 1024L;

        /// <summary>
        /// Binary mebibyte factor used to express multipart framing allowance in readable IEC units.
        /// </summary>
        private const long BYTES_PER_MEBIBYTE = BYTES_PER_KIBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Extra 1 MiB framing margin above <c>MaxImageSizeBytes</c> so valid max-size uploads and form fields survive Kestrel multipart parsing and reach application validation.
        /// </summary>
        public const long MULTIPART_ENVELOPE_OVERHEAD_BYTES = BYTES_PER_MEBIBYTE;

        /// <summary>
        /// Major version number for the initial v1 ContentService HTTP API contract.
        /// </summary>
        public const int API_VERSION_MAJOR = 1;

        /// <summary>
        /// Minor version number for the initial v1 ContentService HTTP API contract.
        /// </summary>
        public const int API_VERSION_MINOR = 0;

        /// <summary>
        /// ApiExplorer format string (<c>'v'VVV</c>) so OpenAPI documents and Swagger UI group endpoints under <c>v1</c>, <c>v2</c>, etc.
        /// </summary>
        public const string API_EXPLORER_GROUP_NAME_FORMAT = "'v'VVV";

        /// <summary>
        /// Versioned API root group template so all ContentService endpoints require an explicit URL segment version (e.g., <c>/api/v1/...</c>).
        /// </summary>
        public const string VERSIONED_API_ROUTE_PREFIX = "/api/v{version:apiVersion}";

        /// <summary>
        /// Concrete v1 base path for image resources used when emitting <c>201 Created</c> <c>Location</c> headers (never a route template).
        /// </summary>
        public const string V1_IMAGES_ROUTE_PREFIX = "/api/v1/images";

        /// <summary>
        /// Concrete v1 base path for template resources used when emitting <c>201 Created</c> <c>Location</c> headers (never a route template).
        /// </summary>
        public const string V1_TEMPLATES_ROUTE_PREFIX = "/api/v1/templates";

        /// <summary>
        /// Relative route segment for image endpoints under <see cref="VERSIONED_API_ROUTE_PREFIX"/>.
        /// </summary>
        public const string IMAGES_GROUP_RELATIVE_ROUTE = "/images";

        /// <summary>
        /// Relative route segment for template endpoints under <see cref="VERSIONED_API_ROUTE_PREFIX"/>.
        /// </summary>
        public const string TEMPLATES_GROUP_RELATIVE_ROUTE = "/templates";

        /// <summary>
        /// Dedicated RPC-style route under <see cref="VERSIONED_API_ROUTE_PREFIX"/> used by editors and publishers to assemble checksum-verified offline kiosk bundles.
        /// </summary>
        public const string PUBLISH_BUNDLES_RELATIVE_ROUTE = "/publish-bundles/resolve";

        /// <summary>
        /// Diagnostic route under <see cref="VERSIONED_API_ROUTE_PREFIX"/> allowing admin UIs and health probes to surface quota warnings before uploads hit <c>507 Insufficient Storage</c>.
        /// </summary>
        public const string STORAGE_HEALTH_RELATIVE_ROUTE = "/health/storage";

        /// <summary>
        /// Version-neutral process liveness/readiness probe route for Dapr sidecars and container orchestrators.
        /// </summary>
        public const string LIVENESS_HEALTH_ROUTE = "/healthz";

        /// <summary>
        /// Header attached to Dapr-forwarded cross-node requests so a misconfigured or stale node never creates an infinite forwarding loop.
        /// </summary>
        public const string NODE_FORWARDED_HEADER_NAME = "X-Node-Forwarded";

        /// <summary>
        /// Header value indicating a request has already been forwarded once across storage nodes.
        /// </summary>
        public const string NODE_FORWARDED_HEADER_VALUE = "true";

        /// <summary>
        /// Group-root route segment avoiding duplicated literal slashes across endpoint group mappings.
        /// </summary>
        public const string GROUP_ROOT_ROUTE = "/";

        /// <summary>
        /// Typed GUID route constraint ensuring malformed non-GUID resource IDs fail fast at routing instead of reaching use cases.
        /// </summary>
        public const string RESOURCE_ID_ROUTE = "/{id:guid}";

        /// <summary>
        /// Shard-aware image route parameter accepting either a bare GUID or a <c>{node}_{guid}</c> external identifier.
        /// </summary>
        public const string IMAGE_IDENTIFIER_ROUTE = "/{imageIdentifier}";

        /// <summary>
        /// Batch metadata lookup route used by peer storage nodes to resolve multiple image IDs in one Dapr call.
        /// </summary>
        public const string IMAGE_BATCH_METADATA_ROUTE = "/batch-metadata";

        /// <summary>
        /// Dedicated sub-resource route separating binary byte streaming from JSON metadata retrieval on <see cref="IMAGE_IDENTIFIER_ROUTE"/>.
        /// </summary>
        public const string IMAGE_CONTENT_ROUTE = "/{imageIdentifier}/content";

        /// <summary>
        /// Sub-resource route serving the saved lightweight preview image for fast gallery rendering.
        /// </summary>
        public const string IMAGE_PREVIEW_ROUTE = "/{imageIdentifier}/preview";

        /// <summary>
        /// Sub-resource route for updating searchable image metadata (description, tags, and album path).
        /// </summary>
        public const string IMAGE_METADATA_ROUTE = "/{imageIdentifier}/metadata";

        /// <summary>
        /// Route listing distinct child album paths under <c>/api/images</c>.
        /// </summary>
        public const string IMAGE_ALBUMS_ROUTE = "/albums";

        /// <summary>
        /// OpenAPI group label keeping image endpoints organized together in generated Swagger docs and client SDKs.
        /// </summary>
        public const string IMAGES_OPENAPI_TAG = "Images";

        /// <summary>
        /// OpenAPI group label keeping template endpoints organized together in generated Swagger docs and client SDKs.
        /// </summary>
        public const string TEMPLATES_OPENAPI_TAG = "Templates";

        /// <summary>
        /// OpenAPI group label separating publishing workflow operations from CRUD resources in API documentation.
        /// </summary>
        public const string PUBLISHING_OPENAPI_TAG = "Publishing";

        /// <summary>
        /// OpenAPI group label isolating operational quota-health diagnostics from content management endpoints.
        /// </summary>
        public const string HEALTH_OPENAPI_TAG = "Health";

        /// <summary>
        /// OpenAPI JSON document endpoint generated by <c>Microsoft.AspNetCore.OpenApi</c> and rendered by Swagger UI.
        /// </summary>
        public const string OPENAPI_DOCUMENT_ROUTE = "/openapi/v1.json";

        /// <summary>
        /// Display title shown in the Swagger UI document selector in Development.
        /// </summary>
        public const string SWAGGER_DOCUMENT_TITLE = "ContentService API v1";

        /// <summary>
        /// Allows browsers and kiosks to cache immutable uploaded images for one year because image bytes never change for an existing ID.
        /// </summary>
        public const string IMAGE_CACHE_CONTROL_HEADER_VALUE = "public, max-age=31536000, immutable";

        /// <summary>
        /// Signals to browsers and media clients that sequential SQL streams do not support random-access byte-range seeking.
        /// </summary>
        public const string ACCEPT_RANGES_NONE = "none";

        /// <summary>
        /// Prevents browsers from MIME-sniffing binary image payloads into executable content types such as HTML or script.
        /// </summary>
        public const string CONTENT_TYPE_OPTIONS_NOSNIFF = "nosniff";

        /// <summary>
        /// Instructs browsers to render images directly inside exhibition pages instead of triggering a file-save download dialog.
        /// </summary>
        public const string INLINE_DISPOSITION_TYPE = "inline";

        /// <summary>
        /// Distinguishes client-aborted image downloads from server faults so cancelled streams do not pollute 5xx error logs or metrics.
        /// </summary>
        public const int CLIENT_CLOSED_REQUEST_STATUS_CODE = 499;

        /// <summary>
        /// Diagnostic fragment used to distinguish ASP.NET Core multipart size-limit rejections (HTTP 413) from malformed form syntax (HTTP 400).
        /// </summary>
        public const string MULTIPART_LENGTH_LIMIT_MESSAGE_FRAGMENT = "Multipart body length limit";
}
