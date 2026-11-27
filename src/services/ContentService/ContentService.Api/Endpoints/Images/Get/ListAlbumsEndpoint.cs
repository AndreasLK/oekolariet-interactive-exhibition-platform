using ContentService.Facade.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles hierarchical image album listing (<c>GET /api/v1/images/albums</c>).
/// </summary>
internal static class ListAlbumsEndpoint
{
        /// <summary>
        /// Lists immediate child album paths directly beneath an optional parent album.
        /// </summary>
        /// <param name="parentPath">Optional parent album path.</param>
        /// <param name="queryFacade">The image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK response containing child album paths.</returns>
        public static async Task<IResult> HandleAsync(
                [FromQuery] string? parentPath,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                IReadOnlyList<string> childAlbums = await queryFacade.ListAlbumsAsync(
                        parentAlbumPath: parentPath,
                        cancellationToken: cancellationToken);

                return Results.Ok(childAlbums);
        }
}

