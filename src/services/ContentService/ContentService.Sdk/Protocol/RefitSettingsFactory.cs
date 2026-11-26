using ContentService.Sdk.Common;
using Refit;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Creates shared <see cref="RefitSettings"/> configured with <c>ContentService.Api</c> JSON defaults,
/// unbuffered streaming, and bounded <see cref="HttpResponseErrorMapper"/> exception translation.
/// </summary>
/// <remarks>
/// Non-standard Refit configuration: Default Refit buffers responses/content and reads full error bodies into
/// <c>ApiException.Content</c>. Here we set <c>Buffered = false</c> and plug in a custom <c>ExceptionFactory</c>.
/// <para>
/// What we gain: Zero-copy streaming for Blazor <c>IBrowserFile</c> uploads (which are non-seekable) and a hard
/// 16 KiB error-body cap that avoids LOH allocations on large proxy error pages.
/// What we lose: Automatic Refit content buffering for diagnostic logging or transparent retries.
/// </para>
/// </remarks>
internal static class RefitSettingsFactory
{
        /// <summary>
        /// Builds <see cref="RefitSettings"/> used by all internal Refit endpoint interfaces in <c>ContentService.Sdk</c>.
        /// </summary>
        /// <returns>A configured <see cref="RefitSettings"/> instance.</returns>
        public static RefitSettings Create() =>
                new()
                {
                        ContentSerializer = new SystemTextJsonContentSerializer(jsonSerializerOptions: SdkConstants.JSON_SERIALIZER_OPTIONS),
                        // Keep Buffered = false so non-seekable streams (such as Blazor IBrowserFile uploads) are streamed directly.
                        Buffered = false,
                        // Wire HttpResponseErrorMapper into ExceptionFactory so Refit enforces the 16 KiB error-body cap before throwing.
                        ExceptionFactory = static async httpResponse => await HttpResponseErrorMapper.CreateExceptionOrNullAsync(
                                response: httpResponse,
                                cancellationToken: CancellationToken.None).ConfigureAwait(false)
                };
}

