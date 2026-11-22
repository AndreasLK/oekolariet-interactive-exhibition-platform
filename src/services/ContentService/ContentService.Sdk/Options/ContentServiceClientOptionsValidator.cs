using ContentService.Sdk.Common;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Options;

/// <summary>
/// Validates <see cref="ContentServiceClientOptions"/> at startup so invalid base URIs, timeouts, or header values fail fast.
/// </summary>
internal sealed class ContentServiceClientOptionsValidator : IValidateOptions<ContentServiceClientOptions>
{
        /// <summary>
        /// Validates the configured <see cref="ContentServiceClientOptions"/> instance.
        /// </summary>
        /// <param name="name">The named options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A validation result indicating success or describing invalid settings.</returns>
        public ValidateOptionsResult Validate(string? name, ContentServiceClientOptions options)
        {
                if (options is null)
                {
                        return ValidateOptionsResult.Fail("ContentService client options are required.");
                }

                if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri)
                {
                        return ValidateOptionsResult.Fail("ContentService BaseAddress must be a non-null absolute URI.");
                }

                bool isHttpScheme = string.Equals(
                        a: options.BaseAddress.Scheme,
                        b: Uri.UriSchemeHttp,
                        comparisonType: StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                                a: options.BaseAddress.Scheme,
                                b: Uri.UriSchemeHttps,
                                comparisonType: StringComparison.OrdinalIgnoreCase);
                if (!isHttpScheme)
                {
                        return ValidateOptionsResult.Fail("ContentService BaseAddress must use HTTP or HTTPS.");
                }

                if (options.RequestTimeout is not null && options.RequestTimeout.Value <= TimeSpan.Zero)
                {
                        return ValidateOptionsResult.Fail("ContentService RequestTimeout must be greater than zero when specified.");
                }

                if (!IsValidOptionalHeaderValue(value: options.DefaultActorId))
                {
                        return ValidateOptionsResult.Fail("ContentService DefaultActorId must contain only printable ASCII characters.");
                }

                return ValidateOptionsResult.Success;
        }

        private static bool IsValidOptionalHeaderValue(string? value)
        {
                if (string.IsNullOrWhiteSpace(value))
                {
                        return true;
                }

                return SdkConstants.IsPrintableAsciiHeaderValue(value: value);
        }
}

