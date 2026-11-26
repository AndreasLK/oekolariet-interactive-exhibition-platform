using System.Net.Http.Headers;
using System.Text;
using ContentService.Sdk.Common;
using ContentService.Sdk.Models;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Validates image upload requests and constructs <see cref="MultipartFormDataContent"/> payloads compatible with <c>ContentService.Api</c>.
/// </summary>
internal static class ImageUploadMultipartContentFactory
{
        /// <summary>
        /// Validates the upload command and builds a multipart form-data payload without inspecting stream length or position.
        /// </summary>
        /// <remarks>
        /// Non-standard multipart construction: Instead of Refit's <c>[Multipart] StreamPart</c>, we build
        /// <see cref="MultipartFormDataContent"/> manually.
        /// <para>
        /// What we gain: Explicit dual <c>FileName</c> (7-bit ASCII fallback) + <c>FileNameStar</c> (UTF-8 Danish characters),
        /// omission of <see langword="null"/> optional fields, and safe handling of non-seekable Blazor <c>IBrowserFile</c>
        /// streams without touching <c>Stream.Length</c> or <c>Stream.Position</c>.
        /// What we lose: Declarative Refit <c>[Multipart]</c> parameter attributes on the endpoint interface.
        /// </para>
        /// </remarks>
        /// <param name="command">The caller-supplied upload stream, file name, MIME type, and optional alt text.</param>
        /// <returns>A configured <see cref="MultipartFormDataContent"/> ready to send to <c>POST api/images</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when the stream is unreadable, the file name is unsafe, or the content type is invalid.</exception>
        public static MultipartFormDataContent Create(ContentUploadImageCommand command)
        {
                // Validate before wrapping command.Content in StreamContent so validation errors never dispose the caller's stream.
                ValidateUploadCommand(command: command);

                MediaTypeHeaderValue parsedMediaType = MediaTypeHeaderValue.Parse(input: command.ContentType);
                MultipartFormDataContent multipart = [];
                StreamContent? fileContent = null;

                try
                {
                        fileContent = new StreamContent(content: command.Content);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType: parsedMediaType.MediaType!);
                        fileContent.Headers.ContentDisposition = BuildFileContentDisposition(fileName: command.FileName);

                        multipart.Add(content: fileContent);
                        // Clear local ownership once MultipartFormDataContent owns fileContent disposal.
                        fileContent = null;

                        AddOptionalFormField(multipart: multipart, fieldName: SdkConstants.MULTIPART_ALT_TEXT_FIELD_NAME, value: command.AltText);
                        AddOptionalFormField(multipart: multipart, fieldName: SdkConstants.MULTIPART_ALBUM_PATH_FIELD_NAME, value: command.AlbumPath);
                        AddOptionalFormField(multipart: multipart, fieldName: SdkConstants.MULTIPART_TAGS_FIELD_NAME, value: command.Tags);
                        return multipart;
                }
                catch
                {
                        fileContent?.Dispose();
                        multipart.Dispose();
                        throw;
                }
        }

        /// <summary>
        /// Validates that the upload command has a readable stream, safe file name, and parseable MIME media type.
        /// </summary>
        /// <param name="command">The upload command to validate.</param>
        private static void ValidateUploadCommand(ContentUploadImageCommand command)
        {
                if (command.Content is null || !command.Content.CanRead)
                {
                        throw new ArgumentException(
                                message: "Upload content stream must be non-null and readable.",
                                paramName: nameof(command));
                }

                if (string.IsNullOrWhiteSpace(command.FileName) || ContainsHeaderUnsafeCharacters(value: command.FileName))
                {
                        throw new ArgumentException(
                                message: "FileName must be non-empty and cannot contain quotes or control characters.",
                                paramName: nameof(command));
                }

                if (!MediaTypeHeaderValue.TryParse(input: command.ContentType, parsedValue: out MediaTypeHeaderValue? parsedType)
                        || string.IsNullOrWhiteSpace(parsedType.MediaType))
                {
                        throw new ArgumentException(
                                message: "ContentType must be a valid MIME media type.",
                                paramName: nameof(command));
                }
        }

        /// <summary>
        /// Builds the multipart <c>Content-Disposition</c> header carrying both an ASCII fallback name and UTF-8 <c>filename*</c>.
        /// </summary>
        /// <param name="fileName">The original UTF-8 file name.</param>
        /// <returns>The configured <see cref="ContentDispositionHeaderValue"/>.</returns>
        private static ContentDispositionHeaderValue BuildFileContentDisposition(string fileName)
        {
                // Preserve UTF-8 Danish characters via FileNameStar while giving legacy parsers a sanitized 7-bit ASCII FileName.
                return new ContentDispositionHeaderValue(dispositionType: SdkConstants.FORM_DATA_DISPOSITION_TYPE)
                {
                        Name = QuoteHeaderToken(value: SdkConstants.MULTIPART_FILE_FIELD_NAME),
                        FileName = QuoteHeaderToken(value: BuildAsciiFallbackFileName(fileName: fileName)),
                        FileNameStar = fileName
                };
        }

        /// <summary>
        /// Appends an optional text form field only when non-null so omitted values stay <see langword="null"/> server-side.
        /// </summary>
        /// <param name="multipart">The target multipart container.</param>
        /// <param name="fieldName">The form field name expected by <c>ContentService.Api</c>.</param>
        /// <param name="value">The optional field value.</param>
        private static void AddOptionalFormField(
                MultipartFormDataContent multipart,
                string fieldName,
                string? value)
        {
                if (value is null)
                {
                        return;
                }

                StringContent fieldContent = new(content: value);
                fieldContent.Headers.ContentDisposition = new ContentDispositionHeaderValue(
                        dispositionType: SdkConstants.FORM_DATA_DISPOSITION_TYPE)
                {
                        Name = QuoteHeaderToken(value: fieldName)
                };
                multipart.Add(content: fieldContent);
        }

        /// <summary>
        /// Checks whether a file name contains quotes or control characters that could corrupt multipart headers.
        /// </summary>
        /// <param name="value">The candidate file name.</param>
        /// <returns><see langword="true"/> if unsafe characters exist; otherwise, <see langword="false"/>.</returns>
        private static bool ContainsHeaderUnsafeCharacters(string value)
        {
                foreach (char character in value)
                {
                        if (char.IsControl(character) || character == '"')
                        {
                                return true;
                        }
                }

                return false;
        }

        /// <summary>
        /// Produces a 7-bit printable ASCII fallback file name by replacing non-ASCII characters.
        /// </summary>
        /// <param name="fileName">The original UTF-8 file name.</param>
        /// <returns>A non-empty ASCII-safe file name.</returns>
        private static string BuildAsciiFallbackFileName(string fileName)
        {
                StringBuilder builder = new(capacity: fileName.Length);
                foreach (char character in fileName)
                {
                        bool isSafeAscii = character >= SdkConstants.MIN_PRINTABLE_ASCII
                                && character <= SdkConstants.MAX_PRINTABLE_ASCII
                                && character != '"';
                        builder.Append(isSafeAscii ? character : SdkConstants.ASCII_FALLBACK_CHAR);
                }

                string sanitized = builder.ToString().Trim();
                return string.IsNullOrEmpty(sanitized)
                        ? SdkConstants.FALLBACK_ASCII_FILENAME
                        : sanitized;
        }

        /// <summary>
        /// Wraps a header token in double quotes as required by HTTP multipart disposition syntax.
        /// </summary>
        /// <param name="value">The token to quote.</param>
        /// <returns>The quoted token string.</returns>
        private static string QuoteHeaderToken(string value)
        {
                return $"\"{value}\"";
        }
}

