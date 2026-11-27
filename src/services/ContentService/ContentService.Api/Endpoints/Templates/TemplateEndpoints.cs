using ContentService.Api.Common;
using ContentService.Api.Endpoints.Templates;
using ContentService.Sdk.Models;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Wires the <c>/api/v1/templates</c> route group to individual template lifecycle endpoint handlers.
/// </summary>
internal static class TemplateEndpoints
{
        /// <summary>
        /// Maps the <c>/api/v1/templates</c> route group with explicit OpenAPI status contracts so generated clients handle validation, missing resources, and concurrency conflicts cleanly.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated route group builder for further endpoint metadata configuration.</returns>
        public static RouteGroupBuilder MapTemplateEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                // Create the /templates route group under the versioned /api/v1 prefix.
                RouteGroupBuilder group = endpoints.MapGroup(ApiConstants.TEMPLATES_GROUP_RELATIVE_ROUTE)
                        .WithTags(ApiConstants.TEMPLATES_OPENAPI_TAG);

                // Map CRUD and image-reference endpoints with explicit OpenAPI status responses.
                group.MapPost(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: CreateTemplateEndpoint.HandleAsync)
                        .Produces<ContentTemplateResponse>(StatusCodes.Status201Created)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: ListTemplatesEndpoint.HandleAsync)
                        .Produces<ContentPagedResponse<ContentTemplateResponse>>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapGet(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: GetTemplateEndpoint.HandleAsync)
                        .Produces<ContentTemplateResponse>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: "/image-references/{id:guid}", handler: CheckImageTemplateReferenceEndpoint.HandleAsync)
                        .Produces<ContentImageTemplateReferenceResponse>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapPut(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: UpdateTemplateEndpoint.HandleAsync)
                        .Produces<ContentTemplateResponse>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                group.MapDelete(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: DeleteTemplateEndpoint.HandleAsync)
                        .Produces(StatusCodes.Status204NoContent)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                return group;
        }
}

