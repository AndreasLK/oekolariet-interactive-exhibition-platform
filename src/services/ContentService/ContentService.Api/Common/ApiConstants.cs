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
        /// Shared base route for image endpoints so route registration and <c>201 Created</c> <c>Location</c> URIs never drift apart.
        /// </summary>
        public const string IMAGES_ROUTE_PREFIX = "/api/images";

        /// <summary>
        /// Shared base route for template endpoints so route registration and <c>201 Created</c> <c>Location</c> URIs never drift apart.
        /// </summary>
        public const string TEMPLATES_ROUTE_PREFIX = "/api/templates";

        /// <summary>
        /// Dedicated RPC-style route used by editors and publishers to assemble checksum-verified offline kiosk bundles.
        /// </summary>
        public const string PUBLISH_BUNDLES_ROUTE = "/api/publish-bundles/resolve";

        /// <summary>
        /// Diagnostic route allowing admin UIs and health probes to surface quota warnings before uploads hit <c>507 Insufficient Storage</c>.
        /// </summary>
        public const string STORAGE_HEALTH_ROUTE = "/api/health/storage";

        /// <summary>
        /// Group-root route segment avoiding duplicated literal slashes across endpoint group mappings.
        /// </summary>
        public const string GROUP_ROOT_ROUTE = "/";

        /// <summary>
        /// Typed GUID route constraint ensuring malformed non-GUID resource IDs fail fast at routing instead of reaching use cases.
        /// </summary>
        public const string RESOURCE_ID_ROUTE = "/{id:guid}";

        /// <summary>
        /// Dedicated sub-resource route separating binary byte streaming from JSON metadata retrieval on <see cref="RESOURCE_ID_ROUTE"/>.
        /// </summary>
        public const string IMAGE_CONTENT_ROUTE = "/{id:guid}/content";

        /// <summary>
        /// Sub-resource route for updating searchable image metadata (description, tags, and album path).
        /// </summary>
        public const string IMAGE_METADATA_ROUTE = "/{id:guid}/metadata";

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
