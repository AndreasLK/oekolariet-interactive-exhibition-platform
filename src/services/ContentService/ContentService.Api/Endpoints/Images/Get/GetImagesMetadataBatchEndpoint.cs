using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles batch image metadata lookups (<c>POST /api/v1/images/batch-metadata</c>).
/// </summary>
internal static class GetImagesMetadataBatchEndpoint
{
        /// <summary>
        /// Batch-retrieves active image metadata for peer storage nodes verifying cross-node template or bundle references.
        /// </summary>
        /// <param name="imageIds">The image identifiers to query.</param>
        /// <param name="queryFacade">The image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK response containing matching <see cref="ContentImageResponse"/> entries.</returns>
        public static async Task<IResult> HandleAsync(
                IReadOnlyCollection<Guid> imageIds,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                IReadOnlyList<ImageMetadataDto> results = await queryFacade.GetImagesMetadataBatchAsync(
                        imageIds: imageIds,
                        cancellationToken: cancellationToken);

                return Results.Ok(value: ImageMapper.ToResponseList(dtos: results));
        }
}

