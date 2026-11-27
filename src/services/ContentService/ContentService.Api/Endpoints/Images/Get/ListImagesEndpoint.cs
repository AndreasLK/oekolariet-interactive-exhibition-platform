using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;
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
        /// <returns>An HTTP 200 OK response containing a <see cref="ContentPagedResponse{T}"/> of image metadata.</returns>
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
                ListImagesQueryDto query = ImageMapper.ToQuery(
                        page: page,
                        pageSize: pageSize,
                        searchTerm: searchTerm,
                        albumPath: albumPath,
                        includeSubalbums: includeSubalbums);

                // Query the paginated image catalog and return the mapped SDK response.
                PagedResultDto<ImageMetadataDto> result = await queryFacade.ListImagesAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(value: ImageMapper.ToPagedResponse(pagedDto: result));
        }
}

