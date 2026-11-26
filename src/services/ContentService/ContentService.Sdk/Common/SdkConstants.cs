using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentService.Sdk.Common;

/// <summary>
/// Centralizes public consumer defaults and internal HTTP protocol constants for <c>ContentService.Sdk</c>.
/// </summary>
public static class SdkConstants
{
        /// <summary>
        /// Default configuration section name for binding <see cref="Options.ContentServiceClientOptions"/>.
        /// </summary>
        public const string CONTENT_SERVICE_SECTION_NAME = "ContentService";

        /// <summary>
        /// Default URL-segment API version targeted by the SDK when no override is configured.
        /// </summary>
        public const string DEFAULT_API_VERSION = "v1";

        /// <summary>
        /// Default 1-based page index requested when listing exhibition images or templates.
        /// </summary>
        public const int DEFAULT_PAGE = 1;

        /// <summary>
        /// Default number of records requested per catalog page.
        /// </summary>
        public const int DEFAULT_PAGE_SIZE = 25;

        /// <summary>
        /// Header name used to attribute mutating ContentService operations (uploads, updates, archival, and deletions) to an internal actor.
        /// </summary>
        public const string ACTOR_HEADER_NAME = "X-Actor-Id";

        /// <summary>
        /// Root API path segment (omits leading slash so subpath base URIs are preserved).
        /// </summary>
        internal const string API_ROUTE_SEGMENT = "api";

        /// <summary>
        /// Image resource path segment appended after <c>api/{version}</c>.
        /// </summary>
        internal const string IMAGES_RESOURCE_SEGMENT = "images";

        /// <summary>
        /// Template resource path segment appended after <c>api/{version}</c>.
        /// </summary>
        internal const string TEMPLATES_RESOURCE_SEGMENT = "templates";

        /// <summary>
        /// Publish bundle resolution path segment appended after <c>api/{version}</c>.
        /// </summary>
        internal const string PUBLISH_BUNDLES_RESOLVE_PATH = "publish-bundles/resolve";

        /// <summary>
        /// Storage health diagnostic path segment appended after <c>api/{version}</c>.
        /// </summary>
        internal const string STORAGE_HEALTH_PATH = "health/storage";

        /// <summary>
        /// Pool upload-offer scatter-gather path segment appended after <c>api/{version}</c>.
        /// </summary>
        internal const string POOL_UPLOAD_OFFERS_PATH = "pool/upload-offers";

        /// <summary>
        /// Route suffix appended to an image resource path to retrieve its binary payload stream.
        /// </summary>
        internal const string IMAGE_CONTENT_SUFFIX = "content";

        /// <summary>
        /// Route suffix appended to an image resource path to retrieve its lightweight preview stream.
        /// </summary>
        internal const string IMAGE_PREVIEW_SUFFIX = "preview";

        /// <summary>
        /// Route suffix appended to <c>api/{version}/images</c> for batch metadata lookups.
        /// </summary>
        internal const string IMAGE_BATCH_METADATA_SUFFIX = "batch-metadata";

        /// <summary>
        /// Route suffix appended to an image resource path to update searchable metadata.
        /// </summary>
        internal const string IMAGE_METADATA_SUFFIX = "metadata";

        /// <summary>
        /// Route suffix appended to <c>api/{version}/images</c> to list immediate child album paths.
        /// </summary>
        internal const string IMAGE_ALBUMS_SUFFIX = "albums";

        /// <summary>
        /// Route suffix appended to <c>api/{version}/templates</c> to check image-to-template references.
        /// </summary>
        internal const string TEMPLATE_IMAGE_REFERENCES_SUFFIX = "image-references";

        /// <summary>
        /// Query string parameter name for the 1-based page index on catalog listings.
        /// </summary>
        internal const string PAGE_QUERY_PARAM = "page";

        /// <summary>
        /// Query string parameter name for the page size on catalog listings.
        /// </summary>
        internal const string PAGE_SIZE_QUERY_PARAM = "pageSize";

        /// <summary>
        /// Query string parameter name for search text on catalog listings.
        /// </summary>
        internal const string SEARCH_TERM_QUERY_PARAM = "searchTerm";

        /// <summary>
        /// Query string parameter name for album path filtering on image listings.
        /// </summary>
        internal const string ALBUM_PATH_QUERY_PARAM = "albumPath";

        /// <summary>
        /// Query string parameter name controlling recursive sub-album inclusion on image listings.
        /// </summary>
        internal const string INCLUDE_SUBALBUMS_QUERY_PARAM = "includeSubalbums";

        /// <summary>
        /// Query string parameter name for the parent album path on album listings.
        /// </summary>
        internal const string PARENT_PATH_QUERY_PARAM = "parentPath";

        /// <summary>
        /// Query string parameter name for filtering templates by component type.
        /// </summary>
        internal const string COMPONENT_TYPE_QUERY_PARAM = "componentType";

        /// <summary>
        /// Form field name bound by ContentService <c>IFormFile file</c> upload parameter.
        /// </summary>
        internal const string MULTIPART_FILE_FIELD_NAME = "file";

        /// <summary>
        /// Form field name bound by ContentService <c>[FromForm] string? altText</c> upload parameter.
        /// </summary>
        internal const string MULTIPART_ALT_TEXT_FIELD_NAME = "altText";

        /// <summary>
        /// Form field name bound by ContentService <c>[FromForm] string? albumPath</c> upload parameter.
        /// </summary>
        internal const string MULTIPART_ALBUM_PATH_FIELD_NAME = "albumPath";

        /// <summary>
        /// Form field name bound by ContentService <c>[FromForm] string? tags</c> upload parameter.
        /// </summary>
        internal const string MULTIPART_TAGS_FIELD_NAME = "tags";

        /// <summary>
        /// Content-Disposition type required by ASP.NET Core multipart form-data parsing.
        /// </summary>
        internal const string FORM_DATA_DISPOSITION_TYPE = "form-data";

        /// <summary>
        /// Fallback ASCII filename used alongside UTF-8 <c>FileNameStar</c> when an upload filename contains non-ASCII characters.
        /// </summary>
        internal const string FALLBACK_ASCII_FILENAME = "image";

        /// <summary>
        /// Media type indicating an RFC 7807 ProblemDetails JSON response.
        /// </summary>
        internal const string PROBLEM_JSON_MEDIA_TYPE = "application/problem+json";

        /// <summary>
        /// Standard JSON media type.
        /// </summary>
        internal const string JSON_MEDIA_TYPE = "application/json";

        /// <summary>
        /// Media type suffix for structured JSON extensions such as <c>+json</c>.
        /// </summary>
        internal const string JSON_MEDIA_TYPE_SUFFIX = "+json";

        /// <summary>
        /// Maximum number of bytes read from an error response body when constructing diagnostics.
        /// </summary>
        internal const int MAX_ERROR_BODY_BYTES = 16 * 1024;

        /// <summary>
        /// Fallback ASCII replacement character when building a 7-bit <c>filename</c> header parameter.
        /// </summary>
        internal const char ASCII_FALLBACK_CHAR = '_';

        /// <summary>
        /// Highest valid 7-bit printable ASCII code point permitted in HTTP header tokens and fallback filenames.
        /// </summary>
        internal const int MAX_PRINTABLE_ASCII = 126;

        /// <summary>
        /// Lowest printable ASCII code point (space).
        /// </summary>
        internal const int MIN_PRINTABLE_ASCII = 32;

        /// <summary>
        /// Shared JSON serializer configuration matching <c>ContentService.Api</c> web defaults and string enums.
        /// </summary>
        internal static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = CreateJsonSerializerOptions();

        /// <summary>
        /// Checks whether an optional HTTP header value contains only printable 7-bit ASCII characters without control characters.
        /// </summary>
        /// <param name="value">The header value to validate.</param>
        /// <returns><see langword="true"/> when all characters are printable ASCII; otherwise, <see langword="false"/>.</returns>
        internal static bool IsPrintableAsciiHeaderValue(string value)
        {
                foreach (char character in value)
                {
                        // Reject non-ASCII and control characters (such as CR/LF) before HttpRequestHeaders throws FormatException.
                        if (character < MIN_PRINTABLE_ASCII || character > MAX_PRINTABLE_ASCII)
                        {
                                return false;
                        }
                }

                return true;
        }

        /// <summary>
        /// Creates the shared <see cref="JsonSerializerOptions"/> matching <c>ContentService.Api</c> Minimal API defaults.
        /// </summary>
        /// <returns>A configured <see cref="JsonSerializerOptions"/> instance with string enum serialization.</returns>
        private static JsonSerializerOptions CreateJsonSerializerOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                // ContentService.Api serializes enums as strings; match that contract so Refit queries/bodies round-trip cleanly.
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}
