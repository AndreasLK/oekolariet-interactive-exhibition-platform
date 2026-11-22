using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;

namespace ContentService.Tests.TestDoubles;

/// <summary>
/// Configurable stub <see cref="IContentServiceImageClient"/> for bUnit component tests.
/// </summary>
internal sealed class StubContentImageClient : IContentServiceImageClient
{
        private const int DEFAULT_PAGE = 1;
        private const int DEFAULT_PAGE_SIZE = 25;

        public Func<ContentImageQuery, CancellationToken, Task<ContentPagedResult<ContentImageMetadata>>>? OnListImagesQueryAsync { get; init; }

        public Func<string?, CancellationToken, Task<IReadOnlyList<string>>>? OnListAlbumsAsync { get; init; }

        public Func<UploadImageRequest, CancellationToken, Task<ContentImageMetadata>>? OnUploadImageAsync { get; init; }

        public Task<ContentPagedResult<ContentImageMetadata>> ListImagesAsync(
                int page = DEFAULT_PAGE,
                int pageSize = DEFAULT_PAGE_SIZE,
                string? searchTerm = null,
                CancellationToken cancellationToken = default) =>
                this.ListImagesAsync(
                        query: new ContentImageQuery
                        {
                                Page = page,
                                PageSize = pageSize,
                                SearchTerm = searchTerm
                        },
                        cancellationToken: cancellationToken);

        public Task<ContentPagedResult<ContentImageMetadata>> ListImagesAsync(
                ContentImageQuery query,
                CancellationToken cancellationToken = default) =>
                this.OnListImagesQueryAsync is null
                        ? Task.FromResult(result: new ContentPagedResult<ContentImageMetadata>
                        {
                                Items = [],
                                Page = query.Page,
                                PageSize = query.PageSize,
                                TotalCount = 0
                        })
                        : this.OnListImagesQueryAsync(query, cancellationToken);

        public Task<IReadOnlyList<string>> ListAlbumsAsync(
                string? parentPath = null,
                CancellationToken cancellationToken = default) =>
                this.OnListAlbumsAsync is null
                        ? Task.FromResult<IReadOnlyList<string>>(result: [])
                        : this.OnListAlbumsAsync(parentPath, cancellationToken);

        public Task<ContentImageMetadata> UploadImageAsync(
                UploadImageRequest request,
                CancellationToken cancellationToken = default) =>
                this.OnUploadImageAsync is null
                        ? throw new NotSupportedException()
                        : this.OnUploadImageAsync(request, cancellationToken);

        public Task<ContentImageMetadata> UpdateImageMetadataAsync(
                Guid imageId,
                UpdateImageMetadataRequest request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageDownload> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageMetadata> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task DeleteImageAsync(
                Guid imageId,
                string? actorId = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Uri GetImageContentUri(Guid imageId) =>
                new(uriString: $"https://localhost:49397/api/images/{imageId:D}/content", uriKind: UriKind.Absolute);
}

