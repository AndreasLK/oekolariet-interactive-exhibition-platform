using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

namespace ContentService.Api.Endpoints.Templates;

/// <summary>
/// Handles single template retrieval (<c>GET /api/v1/templates/{id}</c>).
/// </summary>
internal static class GetTemplateEndpoint
{
        /// <summary>
        /// Retrieves a template (including archived ones) by route ID so active drafts and published bundles referencing archived templates continue resolving cleanly.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="queryFacade">The CQS template query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during lookup.</param>
        /// <returns>An HTTP 200 OK response containing the mapped template response.</returns>
        public static async Task<IResult> HandleAsync(
                Guid id,
                ITemplateQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                TemplateMetadataDto template = await queryFacade.GetTemplateAsync(
                        templateId: id,
                        cancellationToken: cancellationToken);

                return Results.Ok(value: TemplateMapper.ToResponse(dto: template));
        }
}

