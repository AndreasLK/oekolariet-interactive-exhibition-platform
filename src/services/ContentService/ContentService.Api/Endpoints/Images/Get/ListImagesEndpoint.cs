using ContentService.Facade.Common;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles paginated exhibition image catalog queries (<c>GET /api/v1/images</c>).
/// </summary>
internal static class ListImagesEndpoint
{
        /// <summary>
        /// Applies default pagination bounds before querying <see cref="IImageQueryFacade"/> so catalog browsing cannot trigger unbounded database reads.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="searchTerm">Optional search term.</param>
        /// <param name="albumPath">Optional album filter.</param>
        /// <param name="includeSubalbums">Whether to include nested subalbums.</param>
        /// <param name="queryFacade">The image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK response containing a <see cref="PagedResultDto{T}"/> of image metadata.</returns>
        public static async Task<IResult> HandleAsync(
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] string? searchTerm,
                [FromQuery] string? albumPath,
                [FromQuery] bool? includeSubalbums,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                // Apply default pagination and include subalbums unless explicitly disabled by the caller.
                ListImagesQueryDto query = new()
                {
                        Page = page ?? FacadeConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? FacadeConstants.DEFAULT_PAGE_SIZE,
                        SearchTerm = searchTerm,
                        AlbumPath = albumPath,
                        IncludeSubalbums = includeSubalbums ?? true
                };

                // Query the paginated image catalog and return 200 OK.
                PagedResultDto<ImageMetadataDto> result = await queryFacade.ListImagesAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(result);
        }
}

