using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using Microsoft.Extensions.Options;

namespace ContentService.Components.Internal;

/// <summary>
/// Coordinates cancellable parallel gallery catalog fetches and recoverable error formatting for <c>ContentImageGallery</c>.
/// </summary>
/// <remarks>
/// Non-standard cancellation hand-off: Uses lock-free <c>Interlocked.Exchange</c> and cancels each displaced
/// <see cref="CancellationTokenSource"/> before disposing it.
/// <para>
/// What we gain: Rapid keystrokes or component unmount never race on CTS disposal, and in-flight awaits observe
/// <see cref="OperationCanceledException"/> rather than <see cref="ObjectDisposedException"/>.
/// What we lose: Callers must check <see cref="IsDisposed"/> and <c>cancellationToken.IsCancellationRequested</c>
/// after <see langword="await"/> to avoid writing stale results into UI state.
/// </para>
/// </remarks>
internal sealed class GalleryLoadCoordinator : IDisposable
{
        private CancellationTokenSource? _loadCts;
        private bool _isDisposed;

        /// <summary>
        /// Gets a value indicating whether this coordinator has been disposed.
        /// </summary>
        public bool IsDisposed => this._isDisposed;

        /// <summary>
        /// Atomically swaps in a fresh <see cref="CancellationTokenSource"/> and cancels/disposes the previous one so only one query owns UI state.
        /// </summary>
        /// <returns>The active cancellation token for the new request, or an already-canceled token when disposed.</returns>
        public CancellationToken BeginNewRequestToken()
        {
                if (this._isDisposed)
                {
                        return new CancellationToken(canceled: true);
                }

                CancellationTokenSource nextCts = new();

                // Interlocked.Exchange guarantees atomic swap+read even if two UI events overlap, so the displaced CTS is never lost or disposed twice.
                CancellationTokenSource? previousCts = Interlocked.Exchange(location1: ref this._loadCts, value: nextCts);
                DisposeTokenSource(tokenSource: previousCts);
                return nextCts.Token;
        }

        /// <summary>
        /// Executes image listing and child album listing in parallel for the requested query.
        /// </summary>
        /// <param name="imageClient">The ContentService image SDK client.</param>
        /// <param name="query">The paginated image query criteria.</param>
        /// <param name="cancellationToken">The token cancelling superseded requests.</param>
        /// <returns>The fetched image page and sorted immediate child album paths.</returns>
        public static async Task<(ContentPagedResponse<ContentImageResponse> Images, IReadOnlyList<string> ChildAlbums)> FetchCatalogAsync(
                IContentServiceImageClient imageClient,
                ContentImageQuery query,
                CancellationToken cancellationToken)
        {
                // Start both independent HTTP calls before awaiting so total wait time equals the slower request instead of their sum.
                Task<ContentPagedResponse<ContentImageResponse>> imagesTask = imageClient.ListImagesAsync(
                        query: query,
                        cancellationToken: cancellationToken);
                Task<IReadOnlyList<string>> albumsTask = imageClient.ListAlbumsAsync(
                        parentPath: query.AlbumPath,
                        cancellationToken: cancellationToken);

                await Task.WhenAll(imagesTask, albumsTask);
                return (await imagesTask, await albumsTask);
        }

        /// <summary>
        /// Determines whether an exception represents a recoverable API, transport, or configuration error suitable for toast reporting.
        /// </summary>
        /// <param name="exception">The caught exception.</param>
        /// <returns><see langword="true"/> when recoverable; otherwise <see langword="false"/>.</returns>
        public static bool IsRecoverableClientException(Exception exception) =>
                exception is ContentServiceApiException
                        or HttpRequestException
                        or OptionsValidationException
                        or InvalidOperationException;

        /// <summary>
        /// Formats a user-facing error message preferring RFC 7807 <c>ProblemDetails.Detail</c> when available.
        /// </summary>
        /// <param name="exception">The caught recoverable exception.</param>
        /// <param name="operationPrefix">A short contextual prefix describing the failed action.</param>
        /// <returns>The formatted error message.</returns>
        public static string FormatClientError(
                Exception exception,
                string operationPrefix) =>
                exception is ContentServiceApiException apiException
                        ? apiException.ProblemDetails?.Detail ?? apiException.Message
                        : $"{operationPrefix}: {exception.Message}";

        /// <summary>
        /// Cancels and disposes any active request token when the owning component unmounts.
        /// </summary>
        public void Dispose()
        {
                // Set disposed flag before canceling so in-flight awaits exit without mutating UI state.
                this._isDisposed = true;
                CancellationTokenSource? activeCts = Interlocked.Exchange(location1: ref this._loadCts, value: null);
                DisposeTokenSource(tokenSource: activeCts);
        }

        private static void DisposeTokenSource(CancellationTokenSource? tokenSource)
        {
                if (tokenSource is null)
                {
                        return;
                }

                // Cancel BEFORE disposing so awaiting HTTP/Delay continuations observe OperationCanceledException instead of ObjectDisposedException.
                tokenSource.Cancel();
                tokenSource.Dispose();
        }
}

