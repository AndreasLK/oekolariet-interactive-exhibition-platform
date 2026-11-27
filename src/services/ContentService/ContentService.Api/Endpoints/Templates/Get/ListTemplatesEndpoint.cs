using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints.Templates;

/// <summary>
/// Handles paginated exhibition template listing (<c>GET /api/v1/templates</c>).
/// </summary>
internal static class ListTemplatesEndpoint
{
        /// <summary>
        /// Applies default pagination bounds before querying <see cref="ITemplateQueryFacade"/> so unbounded catalog queries cannot overload the API or database.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="componentType">Optional component type filter.</param>
        /// <param name="searchTerm">Optional template name search filter.</param>
        /// <param name="queryFacade">The CQS template query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during listing.</param>
        /// <returns>An HTTP 200 OK response containing a <see cref="ContentPagedResponse{T}"/> of templates.</returns>
        public static async Task<IResult> HandleAsync(
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] ContentTemplateComponentType? componentType,
                [FromQuery] string? searchTerm,
                ITemplateQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                // Apply default pagination bounds when query parameters are omitted.
                ListTemplatesQueryDto query = TemplateMapper.ToQuery(
                        page: page,
                        pageSize: pageSize,
                        componentType: componentType,
                        searchTerm: searchTerm);

                // Query the paginated template catalog and return the mapped SDK response.
                PagedResultDto<TemplateMetadataDto> result = await queryFacade.ListTemplatesAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(value: TemplateMapper.ToPagedResponse(pagedDto: result));
        }
}

