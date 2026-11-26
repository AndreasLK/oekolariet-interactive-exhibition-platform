using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentService.Sdk.Common;

/// <summary>
/// Centralizes HTTP route segments, multipart field names, headers, and serialization defaults for <c>ContentService.Sdk</c>.
/// </summary>
internal static class SdkConstants
{
        /// <summary>
        /// Default configuration section name for binding <see cref="Options.ContentServiceClientOptions"/>.
        /// </summary>
        public const string CONTENT_SERVICE_SECTION_NAME = "ContentService";

        /// <summary>
        /// Relative route prefix for ContentService image endpoints (omits leading slash so subpath base URIs are preserved).
        /// </summary>
        public const string IMAGES_ROUTE_PREFIX = "api/images";

        /// <summary>
        /// Route suffix appended to an image resource path to retrieve its binary payload stream.
        /// </summary>
        public const string IMAGE_CONTENT_SUFFIX = "content";

        /// <summary>
        /// Route suffix appended to an image resource path to update searchable metadata.
        /// </summary>
        public const string IMAGE_METADATA_SUFFIX = "metadata";

        /// <summary>
        /// Route suffix appended to <c>api/images</c> to list immediate child album paths.
        /// </summary>
        public const string IMAGE_ALBUMS_SUFFIX = "albums";

        /// <summary>
        /// Default 1-based page index requested when listing exhibition images.
        /// </summary>
        public const int DEFAULT_PAGE = 1;

        /// <summary>
        /// Default number of image records requested per catalog page.
        /// </summary>
        public const int DEFAULT_PAGE_SIZE = 25;

        /// <summary>
        /// Query string parameter name for the 1-based page index on <c>GET /api/images</c>.
        /// </summary>
        public const string PAGE_QUERY_PARAM = "page";

        /// <summary>
        /// Query string parameter name for the page size on <c>GET /api/images</c>.
        /// </summary>
        public const string PAGE_SIZE_QUERY_PARAM = "pageSize";

        /// <summary>
        /// Query string parameter name for search text on <c>GET /api/images</c>.
        /// </summary>
        public const string SEARCH_TERM_QUERY_PARAM = "searchTerm";

        /// <summary>
        /// Query string parameter name for album path filtering on <c>GET /api/images</c>.
        /// </summary>
        public const string ALBUM_PATH_QUERY_PARAM = "albumPath";

        /// <summary>
        /// Query string parameter name controlling recursive sub-album inclusion on <c>GET /api/images</c>.
        /// </summary>
        public const string INCLUDE_SUBALBUMS_QUERY_PARAM = "includeSubalbums";

        /// <summary>
        /// Query string parameter name for the parent album path on <c>GET /api/images/albums</c>.
        /// </summary>
        public const string PARENT_PATH_QUERY_PARAM = "parentPath";

        /// <summary>
        /// Form field name bound by ContentService <c>IFormFile file</c> upload parameter.
        /// </summary>
        public const string MULTIPART_FILE_FIELD_NAME = "file";

        /// <summary>
        /// Form field name bound by ContentService <c>[FromForm] string? altText</c> upload parameter.
        /// </summary>
        public const string MULTIPART_ALT_TEXT_FIELD_NAME = "altText";

        /// <summary>
        /// Form field name bound by ContentService <c>[FromForm] string? albumPath</c> upload parameter.
        /// </summary>
        public const string MULTIPART_ALBUM_PATH_FIELD_NAME = "albumPath";

        /// <summary>
        /// Form field name bound by ContentService <c>[FromForm] string? tags</c> upload parameter.
        /// </summary>
        public const string MULTIPART_TAGS_FIELD_NAME = "tags";

        /// <summary>
        /// Header name used to attribute image uploads to an internal editor actor.
        /// </summary>
        public const string ACTOR_HEADER_NAME = "X-Actor-Id";

        /// <summary>
        /// Content-Disposition type required by ASP.NET Core multipart form-data parsing.
        /// </summary>
        public const string FORM_DATA_DISPOSITION_TYPE = "form-data";

        /// <summary>
        /// Fallback ASCII filename used alongside UTF-8 <c>FileNameStar</c> when an upload filename contains non-ASCII characters.
        /// </summary>
        public const string FALLBACK_ASCII_FILENAME = "image";

        /// <summary>
        /// Media type indicating an RFC 7807 ProblemDetails JSON response.
        /// </summary>
        public const string PROBLEM_JSON_MEDIA_TYPE = "application/problem+json";

        /// <summary>
        /// Standard JSON media type.
        /// </summary>
        public const string JSON_MEDIA_TYPE = "application/json";

        /// <summary>
        /// Media type suffix for structured JSON extensions such as <c>+json</c>.
        /// </summary>
        public const string JSON_MEDIA_TYPE_SUFFIX = "+json";

        /// <summary>
        /// Maximum number of bytes read from an error response body when constructing diagnostics.
        /// </summary>
        public const int MAX_ERROR_BODY_BYTES = 16 * 1024;

        /// <summary>
        /// Fallback ASCII replacement character when building a 7-bit <c>filename</c> header parameter.
        /// </summary>
        public const char ASCII_FALLBACK_CHAR = '_';

        /// <summary>
        /// Highest valid 7-bit printable ASCII code point permitted in HTTP header tokens and fallback filenames.
        /// </summary>
        public const int MAX_PRINTABLE_ASCII = 126;

        /// <summary>
        /// Lowest printable ASCII code point (space).
        /// </summary>
        public const int MIN_PRINTABLE_ASCII = 32;

        /// <summary>
        /// Shared JSON serializer configuration matching <c>ContentService.Api</c> web defaults and string enums.
        /// </summary>
        public static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = CreateJsonSerializerOptions();

        /// <summary>
        /// Checks whether an optional HTTP header value contains only printable 7-bit ASCII characters without control characters.
        /// </summary>
        /// <param name="value">The header value to validate.</param>
        /// <returns><see langword="true"/> when all characters are printable ASCII; otherwise, <see langword="false"/>.</returns>
        public static bool IsPrintableAsciiHeaderValue(string value)
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

        private static JsonSerializerOptions CreateJsonSerializerOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}

