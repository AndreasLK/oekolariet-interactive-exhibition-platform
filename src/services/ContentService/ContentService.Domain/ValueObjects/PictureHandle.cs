using System.Text.RegularExpressions;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Represents a validated picture handle referencing an image entity in JSON payloads.
/// </summary>
public readonly record struct PictureHandle
{
        private static readonly Regex PICTURE_HANDLE_REGEX = new(
                pattern: @"picture\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?![0-9a-fA-F-])",
                options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Initializes a new instance of the <see cref="PictureHandle"/> struct.
        /// </summary>
        /// <param name="imageId">The unique identifier of the referenced image.</param>
        public PictureHandle(Guid imageId)
        {
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Image ID cannot be empty.");
                }

                this.ImageId = imageId;
        }

        /// <summary>
        /// Gets the unique identifier of the referenced image.
        /// </summary>
        public Guid ImageId { get; }

        /// <summary>
        /// Returns the canonical string representation of the picture handle.
        /// </summary>
        /// <returns>A string formatted as <c>picture.&lt;guid&gt;</c>.</returns>
        public override string ToString()
        {
                return $"{DomainConstants.PICTURE_HANDLE_PREFIX}{this.ImageId:D}";
        }

        /// <summary>
        /// Parses a raw picture handle string into a <see cref="PictureHandle"/>.
        /// </summary>
        /// <param name="rawValue">The raw picture handle string.</param>
        /// <returns>The parsed <see cref="PictureHandle"/>.</returns>
        public static PictureHandle Parse(string rawValue)
        {
                if (!TryParse(rawValue: rawValue, handle: out PictureHandle handle))
                {
                        throw new DomainValidationException(
                                message: $"Invalid picture handle format: '{rawValue}'.");
                }

                return handle;
        }

        /// <summary>
        /// Attempts to parse a raw picture handle string into a <see cref="PictureHandle"/>.
        /// </summary>
        /// <param name="rawValue">The raw picture handle string.</param>
        /// <param name="handle">When this method returns, contains the parsed handle if successful.</param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(string rawValue, out PictureHandle handle)
        {
                handle = default;
                if (string.IsNullOrEmpty(value: rawValue))
                {
                        return false;
                }

                ReadOnlySpan<char> span = rawValue.AsSpan();
                ReadOnlySpan<char> prefix = DomainConstants.PICTURE_HANDLE_PREFIX.AsSpan();
                if (!span.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
                {
                        return false;
                }

                ReadOnlySpan<char> guidSpan = span.Slice(start: prefix.Length);
                if (guidSpan.Length != DomainConstants.GUID_D_LENGTH)
                {
                        return false;
                }

                if (!Guid.TryParseExact(input: guidSpan, format: DomainConstants.GUID_FORMAT_HYPHENATED, result: out Guid imageId) || imageId == Guid.Empty)
                {
                        return false;
                }

                handle = new PictureHandle(imageId: imageId);
                return true;
        }

        /// <summary>
        /// Extracts all distinct image identifiers referenced by picture handles in a JSON payload.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload to inspect.</param>
        /// <returns>A list of distinct non-empty image identifiers found in the payload.</returns>
        public static List<Guid> ExtractImageIds(string jsonPayload)
        {
                if (jsonPayload is null)
                {
                        throw new DomainValidationException(message: "JSON payload cannot be null.");
                }

                if (ContainsEmbeddedBase64Image(jsonPayload: jsonPayload))
                {
                        throw new DomainValidationException(
                                message: "Embedded base64 image data URIs are not permitted in JSON payloads.");
                }

                HashSet<Guid> uniqueIds = [];
                ReadOnlySpan<char> payloadSpan = jsonPayload.AsSpan();
                int prefixLength = DomainConstants.PICTURE_HANDLE_PREFIX.Length;

                foreach (ValueMatch match in PICTURE_HANDLE_REGEX.EnumerateMatches(input: payloadSpan))
                {
                        ReadOnlySpan<char> guidSpan = payloadSpan.Slice(start: match.Index + prefixLength, length: DomainConstants.GUID_D_LENGTH);
                        if (Guid.TryParseExact(input: guidSpan, format: DomainConstants.GUID_FORMAT_HYPHENATED, result: out Guid imageId) && imageId != Guid.Empty)
                        {
                                uniqueIds.Add(item: imageId);
                        }
                }

                return [.. uniqueIds];
        }

        private static bool ContainsEmbeddedBase64Image(string jsonPayload)
        {
                return jsonPayload.Contains(value: DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || jsonPayload.Contains(value: DomainConstants.ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
}
