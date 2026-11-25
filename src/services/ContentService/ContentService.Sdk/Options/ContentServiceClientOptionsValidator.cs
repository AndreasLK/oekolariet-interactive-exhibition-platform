using System.Text.RegularExpressions;
using ContentService.Sdk.Common;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Options;

/// <summary>
/// Validates <see cref="ContentServiceClientOptions"/> at startup so invalid base URIs, API versions, timeouts, or header values fail fast.
/// </summary>
internal sealed class ContentServiceClientOptionsValidator : IValidateOptions<ContentServiceClientOptions>
{
        /// <summary>
        /// Regex enforcing safe URL-segment API versions (e.g., <c>v1</c>, <c>v2</c>) without slashes or query characters.
        /// </summary>
        private static readonly Regex API_VERSION_REGEX = new(
                pattern: @"^v\d+$",
                options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// Validates the configured <see cref="ContentServiceClientOptions"/> instance.
        /// </summary>
        /// <param name="name">The named options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A validation result indicating success or describing invalid settings.</returns>
        public ValidateOptionsResult Validate(string? name, ContentServiceClientOptions options)
        {
                // Ensure options object is bound.
                if (options is null)
                {
                        return ValidateOptionsResult.Fail("ContentService client options are required.");
                }

                // Require an absolute base URI so relative endpoint paths resolve deterministically.
                if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri)
                {
                        return ValidateOptionsResult.Fail("ContentService BaseAddress must be a non-null absolute URI.");
                }

                // Restrict to HTTP/HTTPS schemes to prevent accidental file:// or custom URI schemes.
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

                // Reject non-positive timeouts when a custom timeout is configured.
                if (options.RequestTimeout is not null && options.RequestTimeout.Value <= TimeSpan.Zero)
                {
                        return ValidateOptionsResult.Fail("ContentService RequestTimeout must be greater than zero when specified.");
                }

                // Validate optional actor header against printable ASCII to avoid HTTP header injection.
                if (!IsValidOptionalHeaderValue(value: options.DefaultActorId))
                {
                        return ValidateOptionsResult.Fail("ContentService DefaultActorId must contain only printable ASCII characters.");
                }

                // Enforce strict v<major> version segment so URL routing never contains slashes or query characters.
                if (string.IsNullOrWhiteSpace(options.ApiVersion)
                        || !API_VERSION_REGEX.IsMatch(options.ApiVersion.Trim()))
                {
                        return ValidateOptionsResult.Fail("ContentService ApiVersion must match 'v<major>' (e.g., 'v1').");
                }

                return ValidateOptionsResult.Success;
        }

        /// <summary>
        /// Checks that an optional header value is either omitted or contains only printable ASCII characters.
        /// </summary>
        /// <param name="value">The optional header value to inspect.</param>
        /// <returns><see langword="true"/> when blank or valid printable ASCII; otherwise <see langword="false"/>.</returns>
        private static bool IsValidOptionalHeaderValue(string? value)
        {
                // Empty/whitespace actor IDs are treated as unset and fall back to the API default.
                if (string.IsNullOrWhiteSpace(value))
                {
                        return true;
                }

                return SdkConstants.IsPrintableAsciiHeaderValue(value: value);
        }
}

