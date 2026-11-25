using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// Shared constants, multipart upload helpers, and ProblemDetails assertions for SQL Server API integration tests.
/// </summary>
internal static class ApiIntegrationTestFixtures
{
        public const string IMAGES_ROUTE = "/api/v1/images";
        public const string TEMPLATES_ROUTE = "/api/v1/templates";
        public const string PUBLISH_BUNDLES_ROUTE = "/api/v1/publish-bundles/resolve";
        public const string STORAGE_HEALTH_ROUTE = "/api/v1/health/storage";
        public const string MAX_IMAGE_SIZE_CONFIG_KEY = "ContentStorage:MaxImageSizeBytes";
        public const string TOTAL_QUOTA_CONFIG_KEY = "ContentStorage:TotalQuotaBytes";
        public const string WARNING_THRESHOLD_CONFIG_KEY = "ContentStorage:WarningThresholdPercent";
        public const string CRITICAL_THRESHOLD_CONFIG_KEY = "ContentStorage:CriticalThresholdPercent";
        public const string ACTOR_HEADER_NAME = "X-Actor-Id";
        public const string TEST_ACTOR_ID = "api-curator";
        private const string FILE_FORM_FIELD_NAME = "file";
        private const string ALT_TEXT_FORM_FIELD_NAME = "altText";

        /// <summary>
        /// Minimal valid PNG payload: 8-byte PNG magic header followed by 4 dummy payload bytes.
        /// </summary>
        public static readonly byte[] VALID_PNG_PAYLOAD = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

        /// <summary>
        /// Shared JSON serializer options configured with <see cref="JsonStringEnumConverter"/> matching <c>ContentService.Api</c>.
        /// </summary>
        public static readonly JsonSerializerOptions API_JSON_OPTIONS = CreateApiJsonOptions();

        public static async Task<HttpResponseMessage> UploadImageAsync(
                HttpClient client,
                byte[] payload,
                string fileName,
                string contentType,
                string? altText = null)
        {
                using MultipartFormDataContent form = new();
                using ByteArrayContent fileContent = new(payload);
                fileContent.Headers.TryAddWithoutValidation(HeaderNames.ContentType, contentType);
                form.Add(fileContent, FILE_FORM_FIELD_NAME, fileName);

                if (altText is not null)
                {
                        form.Add(new StringContent(altText), ALT_TEXT_FORM_FIELD_NAME);
                }

                using HttpRequestMessage request = new(HttpMethod.Post, IMAGES_ROUTE)
                {
                        Content = form
                };
                request.Headers.Add(ACTOR_HEADER_NAME, TEST_ACTOR_ID);
                return await client.SendAsync(request);
        }

        public static async Task AssertProblemDetailsStatusAsync(
                HttpResponseMessage response,
                HttpStatusCode expectedStatusCode)
        {
                Assert.Equal(expectedStatusCode, response.StatusCode);
                ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(problem);
                Assert.Equal((int)expectedStatusCode, problem.Status);
        }

        private static JsonSerializerOptions CreateApiJsonOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}

