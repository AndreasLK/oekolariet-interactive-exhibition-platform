using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// Shared constants, payloads, and HTTP multipart helpers for Docker-free API endpoint tests.
/// </summary>
internal static class ApiEndpointTestFixtures
{
        public const string NODE_ID = "node-1";
        public const string ALLOWED_ORIGIN = "https://editor.oekolariet.dk";
        public const string DISALLOWED_ORIGIN = "https://evil.example.com";
        public const long SMALL_MAX_IMAGE_BYTES = 1024L;
        public const int MULTIPART_LIMIT_EXCEEDING_BYTES = 1_100_000;

        /// <summary>
        /// Minimal valid PNG test fixture: 8-byte PNG magic header plus 2 dummy body bytes.
        /// </summary>
        public static readonly byte[] VALID_PNG_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

        /// <summary>
        /// Minimal valid JPEG test fixture: SOI + APP0/JFIF marker + 16-byte segment length.
        /// </summary>
        public static readonly byte[] VALID_JPEG_BYTES = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        /// <summary>
        /// Minimal valid WebP test fixture: RIFF + size + WEBP + VP8 marker byte.
        /// </summary>
        public static readonly byte[] VALID_WEBP_BYTES = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56];

        /// <summary>
        /// JSON options matching <c>ContentService.Api</c> string enum configuration.
        /// </summary>
        public static readonly JsonSerializerOptions API_JSON_OPTIONS = CreateApiJsonOptions();

        public static MultipartFormDataContent CreateImageMultipart(
                byte[] bytes,
                string fileName,
                string contentType)
        {
                MultipartFormDataContent form = new();
                ByteArrayContent fileContent = new(bytes);
                fileContent.Headers.TryAddWithoutValidation(name: HeaderNames.ContentType, value: contentType);
                form.Add(content: fileContent, name: "file", fileName: fileName);
                return form;
        }

        public static Task<HttpResponseMessage> UploadFileAsync(
                HttpClient client,
                byte[] bytes,
                string fileName,
                string contentType)
        {
                MultipartFormDataContent form = CreateImageMultipart(bytes: bytes, fileName: fileName, contentType: contentType);
                return client.PostAsync(requestUri: "/api/v1/images", content: form);
        }

        public static ImageEntity CreateSeededImage(
                string fileName,
                byte[] bytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                return ImageEntity.Create(
                        storageNodeId: NODE_ID,
                        fileName: fileName,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: bytes.Length,
                        sha256Hash: hash,
                        actorId: "seed-actor");
        }

        private static JsonSerializerOptions CreateApiJsonOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}

