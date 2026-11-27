using System.Net;
using HighscoreService.Sdk.Exceptions;
using HighscoreService.Sdk.Models.Highscores.Requests;
using HighscoreService.Sdk.Models.Highscores.Responses;
using HighscoreService.Sdk.Models.Statistics.Responses;

namespace HighscoreService.Sdk.Client
{
        /// <summary>
        /// Resilient Highscore Service SDK client that prefers the central Hub and falls back to the local Screen API.
        /// </summary>
        public sealed class HighscoreServiceClient : IHighscoreServiceClient
        {
                private readonly HttpHighscoreEndpointClient _primaryClient;
                private readonly HttpHighscoreEndpointClient? _fallbackClient;
                private readonly TimeSpan _hubTimeout;

                /// <summary>
                /// Initializes a new instance of the <see cref="HighscoreServiceClient"/> class for a single endpoint.
                /// </summary>
                /// <param name="httpClient">Configured HTTP client.</param>
                public HighscoreServiceClient(HttpClient httpClient)
                {
                        httpClient.BaseAddress = NormalizeBaseAddress(httpClient.BaseAddress);
                        this._primaryClient = new HttpHighscoreEndpointClient(httpClient);
                        this._fallbackClient = null;
                        this._hubTimeout = TimeSpan.FromSeconds(HighscoreServiceClientOptions.DEFAULT_HUB_TIMEOUT_SECONDS);
                }

                /// <summary>
                /// Initializes a new instance of the <see cref="HighscoreServiceClient"/> class with Hub-first and Screen fallback.
                /// </summary>
                /// <param name="primaryHttpClient">HTTP client targeting the central Hub.</param>
                /// <param name="fallbackHttpClient">Optional HTTP client targeting the local Screen API.</param>
                /// <param name="hubTimeoutSeconds">Timeout in seconds before failing over from Hub to Screen.</param>
                public HighscoreServiceClient(
                        HttpClient primaryHttpClient,
                        HttpClient? fallbackHttpClient,
                        int hubTimeoutSeconds = HighscoreServiceClientOptions.DEFAULT_HUB_TIMEOUT_SECONDS)
                {
                        primaryHttpClient.BaseAddress = NormalizeBaseAddress(primaryHttpClient.BaseAddress);
                        fallbackHttpClient?.BaseAddress = NormalizeBaseAddress(fallbackHttpClient.BaseAddress);

                        this._primaryClient = new HttpHighscoreEndpointClient(primaryHttpClient);
                        this._fallbackClient = fallbackHttpClient is not null
                                ? new HttpHighscoreEndpointClient(fallbackHttpClient)
                                : null;
                        this._hubTimeout = TimeSpan.FromSeconds(Math.Max(1, hubTimeoutSeconds));
                }

                /// <summary>
                /// Ensures an absolute <see cref="HttpClient.BaseAddress"/> ends with a trailing slash so
                /// RFC 3986 relative URI resolution preserves any reverse-proxy subpath (e.g. <c>/highscore/</c>).
                /// </summary>
                /// <param name="baseAddress">Configured base address.</param>
                /// <returns>The normalized base address, or <see langword="null"/> if unset.</returns>
                internal static Uri? NormalizeBaseAddress(Uri? baseAddress)
                {
                        if (baseAddress is null || !baseAddress.IsAbsoluteUri)
                        {
                                return baseAddress;
                        }

                        // Without a trailing '/', new Uri("https://host/highscore") + "api/v1/highscores"
                        // drops "/highscore" and resolves to "https://host/api/v1/highscores".
                        string absolute = baseAddress.AbsoluteUri;
                        return absolute.EndsWith('/') ? baseAddress : new Uri($"{absolute}/", UriKind.Absolute);
                }

                /// <inheritdoc />
                public Task<HighscoreResponse> RecordAsync(
                        RecordHighscoreRequest request,
                        CancellationToken cancellationToken = default)
                {
                        // NON-STANDARD DECISION: Generate Id (UUIDv7) and PlayedAtUtc client-side before the first attempt.
                        // Pros: Retries and Hub-to-Screen failovers are strictly idempotent and preserve chronological ordering.
                        // Cons: Relies on the game/screen host clock being reasonably synchronized (NTP on exhibition LAN).
                        RecordHighscoreRequest normalized = request with
                        {
                                Id = request.Id ?? Guid.CreateVersion7(),
                                PlayedAtUtc = (request.PlayedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime()
                        };

                        return this.ExecuteWithFallbackAsync(
                                operation: (client, ct) => client.RecordAsync(normalized, ct),
                                cancellationToken: cancellationToken);
                }

                /// <inheritdoc />
                public Task<PagedHighscoreResponse> QueryAsync(
                        HighscoreQueryRequest query,
                        CancellationToken cancellationToken = default)
                {
                        return this.ExecuteWithFallbackAsync(
                                operation: (client, ct) => client.QueryAsync(query, ct),
                                cancellationToken: cancellationToken);
                }

                /// <inheritdoc />
                public Task<HighscoreStatisticsResponse> GetStatisticsAsync(
                        HighscoreQueryRequest query,
                        CancellationToken cancellationToken = default)
                {
                        // NON-STANDARD DECISION: Statistics and administrative deletes never fall back to a local Screen.
                        // Pros: Prevents misleading partial-window analytics (screens prune after 7 days) and split-brain deletes.
                        // Cons: Admin statistics and delete operations are unavailable while the central Hub is offline.
                        return this._primaryClient.GetStatisticsAsync(query, cancellationToken);
                }

                /// <inheritdoc />
                public Task DeleteByIdAsync(
                        Guid id,
                        CancellationToken cancellationToken = default)
                {
                        return this._primaryClient.DeleteByIdAsync(id, cancellationToken);
                }

                /// <inheritdoc />
                public Task DeleteByGameIdAsync(
                        Guid gameId,
                        CancellationToken cancellationToken = default)
                {
                        return this._primaryClient.DeleteByGameIdAsync(gameId, cancellationToken);
                }

                private async Task<TResult> ExecuteWithFallbackAsync<TResult>(
                        Func<HttpHighscoreEndpointClient, CancellationToken, Task<TResult>> operation,
                        CancellationToken cancellationToken)
                {
                        if (this._fallbackClient is null)
                        {
                                return await operation(this._primaryClient, cancellationToken);
                        }

                        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(this._hubTimeout);

                        try
                        {
                                return await operation(this._primaryClient, timeoutCts.Token);
                        }
                        catch (Exception ex) when (IsTransientHubFailure(ex, cancellationToken))
                        {
                                return await operation(this._fallbackClient, cancellationToken);
                        }
                }

                private static bool IsTransientHubFailure(
                        Exception exception,
                        CancellationToken callerCancellationToken)
                {
                        if (callerCancellationToken.IsCancellationRequested)
                        {
                                return false;
                        }

                        return exception switch
                        {
                                HttpRequestException => true,
                                TaskCanceledException => true,
                                HighscoreServiceApiException apiEx => (int)apiEx.StatusCode >= (int)HttpStatusCode.InternalServerError,
                                _ => false
                        };
                }
        }
}
