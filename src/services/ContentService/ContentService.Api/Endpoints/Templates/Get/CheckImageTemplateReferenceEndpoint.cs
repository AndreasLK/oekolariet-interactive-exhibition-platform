using ContentService.Api.Contracts;
using ContentService.Facade.Interfaces;

namespace ContentService.Api.Endpoints.Templates;

/// <summary>
/// Checks whether any active template on this node references a given image (<c>GET /api/v1/templates/image-references/{id}</c>).
/// </summary>
internal static class CheckImageTemplateReferenceEndpoint
{
        /// <summary>
        /// Exposes a lightweight check so peer storage nodes can verify whether any template on this node still references a given image before deleting it.
        /// </summary>
        /// <param name="id">The image identifier to check.</param>
        /// <param name="queryFacade">The CQS template query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK response containing <see cref="ImageReferenceCheckResponse"/>.</returns>
        public static async Task<IResult> HandleAsync(
                Guid id,
                ITemplateQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                bool isReferenced = await queryFacade.IsImageReferencedByAnyTemplateAsync(
                        imageId: id,
                        cancellationToken: cancellationToken);

                return Results.Ok(new ImageReferenceCheckResponse(IsReferenced: isReferenced));
        }
}

