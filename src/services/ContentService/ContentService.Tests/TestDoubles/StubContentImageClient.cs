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

        /// <summary>
        /// Optional delegate overriding <see cref="ListImagesAsync(ContentImageQuery, CancellationToken)"/>.
        /// </summary>
        public Func<ContentImageQuery, CancellationToken, Task<ContentPagedResponse<ContentImageResponse>>>? OnListImagesQueryAsync { get; init; }

        /// <summary>
        /// Optional delegate overriding <see cref="ListAlbumsAsync"/>.
        /// </summary>
        public Func<string?, CancellationToken, Task<IReadOnlyList<string>>>? OnListAlbumsAsync { get; init; }

        /// <summary>
        /// Optional delegate overriding <see cref="UploadImageAsync"/>.
        /// </summary>
        public Func<ContentUploadImageRequest, CancellationToken, Task<ContentImageResponse>>? OnUploadImageAsync { get; init; }

        public Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
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

        public Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
                ContentImageQuery query,
                CancellationToken cancellationToken = default) =>
                this.OnListImagesQueryAsync is null
                        ? Task.FromResult(result: new ContentPagedResponse<ContentImageResponse>
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

        public Task<ContentImageResponse> UploadImageAsync(
                ContentUploadImageRequest request,
                CancellationToken cancellationToken = default) =>
                this.OnUploadImageAsync is null
                        ? throw new NotSupportedException()
                        : this.OnUploadImageAsync(request, cancellationToken);

        public Task<IReadOnlyList<ContentImageResponse>> GetImagesMetadataBatchAsync(
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ContentImageResponse>>(result: []);

        public Task<ContentImageResponse> UpdateImageMetadataAsync(
                Guid imageId,
                ContentUpdateImageMetadataRequest request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageResponse> UpdateImageMetadataAsync(
                string imageIdentifier,
                ContentUpdateImageMetadataRequest request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageDownloadResponse> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageDownloadResponse> DownloadImageAsync(
                string imageIdentifier,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageDownloadResponse> DownloadImagePreviewAsync(
                Guid imageId,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageDownloadResponse> DownloadImagePreviewAsync(
                string imageIdentifier,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageResponse> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task<ContentImageResponse> GetImageMetadataAsync(
                string imageIdentifier,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task DeleteImageAsync(
                Guid imageId,
                string? actorId = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Task DeleteImageAsync(
                string imageIdentifier,
                string? actorId = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

        public Uri GetImageContentUri(Guid imageId) =>
                this.GetImageContentUri(imageIdentifier: imageId.ToString("D"));

        public Uri GetImageContentUri(string imageIdentifier) =>
                new(uriString: $"https://localhost:49397/api/v1/images/{imageIdentifier}/content", uriKind: UriKind.Absolute);

        public Uri GetImagePreviewUri(Guid imageId) =>
                this.GetImagePreviewUri(imageIdentifier: imageId.ToString("D"));

        public Uri GetImagePreviewUri(string imageIdentifier) =>
                new(uriString: $"https://localhost:49397/api/v1/images/{imageIdentifier}/preview", uriKind: UriKind.Absolute);
}

