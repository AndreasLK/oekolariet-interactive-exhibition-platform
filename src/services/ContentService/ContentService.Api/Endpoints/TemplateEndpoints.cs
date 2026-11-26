using ContentService.Api.Common;
using ContentService.Api.Contracts;
using ContentService.Facade.Common;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes exhibition template lifecycle endpoints while keeping concurrency, image-reference indexing, and validation encapsulated behind CQS facades.
/// </summary>
internal static class TemplateEndpoints
{
        /// <summary>
        /// Wires the <c>/api/templates</c> route group with explicit OpenAPI status contracts so generated clients handle validation, missing resources, and concurrency conflicts cleanly.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated route group builder for further endpoint metadata configuration.</returns>
        public static RouteGroupBuilder MapTemplateEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                RouteGroupBuilder group = endpoints.MapGroup(ApiConstants.TEMPLATES_ROUTE_PREFIX)
                        .WithTags(ApiConstants.TEMPLATES_OPENAPI_TAG);

                group.MapPost(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: HandleCreateTemplateAsync)
                        .Produces<TemplateMetadataDto>(StatusCodes.Status201Created)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: HandleListTemplatesAsync)
                        .Produces<PagedResultDto<TemplateMetadataDto>>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapGet(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleGetTemplateAsync)
                        .Produces<TemplateMetadataDto>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapPut(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleUpdateTemplateAsync)
                        .Produces<TemplateMetadataDto>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                group.MapDelete(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleDeleteTemplateAsync)
                        .Produces(StatusCodes.Status204NoContent)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                return group;
        }

        /// <summary>
        /// Translates the HTTP creation body into <see cref="CreateTemplateCommandDto"/> with server-resolved actor identity and returns <c>201 Created</c> with a <c>Location</c> header for immediate follow-up reads.
        /// </summary>
        /// <param name="request">The template creation request payload.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS template command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during persistence.</param>
        /// <returns>An HTTP 201 Created response containing the created <see cref="TemplateMetadataDto"/>.</returns>
        private static async Task<IResult> HandleCreateTemplateAsync(
                CreateTemplateRequest request,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Resolve actor server-side so authenticated claims always override client-supplied ActorId fields.
                CreateTemplateCommandDto command = new()
                {
                        Name = request.Name,
                        ComponentType = request.ComponentType,
                        SchemaJson = request.SchemaJson,
                        ActorId = RequestActorResolver.ResolveActorId(
                                httpContext: httpContext,
                                explicitActorId: request.ActorId)
                };

                TemplateMetadataDto created = await commandFacade.CreateTemplateAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Created(
                        uri: $"{ApiConstants.TEMPLATES_ROUTE_PREFIX}/{created.Id}",
                        value: created);
        }

        /// <summary>
        /// Applies default pagination bounds before querying <see cref="ITemplateQueryFacade"/> so unbounded catalog queries cannot overload the API or database.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="componentType">Optional component type filter.</param>
        /// <param name="searchTerm">Optional template name search filter.</param>
        /// <param name="queryFacade">The CQS template query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during listing.</param>
        /// <returns>An HTTP 200 OK response containing a <see cref="PagedResultDto{T}"/> of templates.</returns>
        private static async Task<IResult> HandleListTemplatesAsync(
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] TemplateComponentTypeDto? componentType,
                [FromQuery] string? searchTerm,
                ITemplateQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                ListTemplatesQueryDto query = new()
                {
                        Page = page ?? FacadeConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? FacadeConstants.DEFAULT_PAGE_SIZE,
                        ComponentType = componentType,
                        SearchTerm = searchTerm
                };

                PagedResultDto<TemplateMetadataDto> result = await queryFacade.ListTemplatesAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(result);
        }

        /// <summary>
        /// Retrieves a template (including archived ones) by route ID so active drafts and published bundles referencing archived templates continue resolving cleanly.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="queryFacade">The CQS template query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during lookup.</param>
        /// <returns>An HTTP 200 OK response containing the template.</returns>
        private static async Task<IResult> HandleGetTemplateAsync(
                Guid id,
                ITemplateQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                TemplateMetadataDto template = await queryFacade.GetTemplateAsync(
                        templateId: id,
                        cancellationToken: cancellationToken);

                return Results.Ok(template);
        }

        /// <summary>
        /// Binds the route ID and expected concurrency stamp into <see cref="UpdateTemplateCommandDto"/> so concurrent editor saves fail with <c>409 Conflict</c> instead of silently overwriting changes.
        /// </summary>
        /// <param name="id">The authoritative template identifier from the URL route.</param>
        /// <param name="request">The template update payload with expected concurrency stamp.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS template command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during transactional update.</param>
        /// <returns>An HTTP 200 OK response containing the updated <see cref="TemplateMetadataDto"/>.</returns>
        private static async Task<IResult> HandleUpdateTemplateAsync(
                Guid id,
                UpdateTemplateRequest request,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                UpdateTemplateCommandDto command = new()
                {
                        TemplateId = id,
                        Name = request.Name,
                        SchemaJson = request.SchemaJson,
                        ExpectedConcurrencyStamp = request.ExpectedConcurrencyStamp,
                        ActorId = RequestActorResolver.ResolveActorId(
                                httpContext: httpContext,
                                explicitActorId: request.ActorId)
                };

                TemplateMetadataDto updated = await commandFacade.UpdateTemplateAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(updated);
        }

        /// <summary>
        /// Archives an active template rather than hard-deleting it so existing exhibition drafts keep resolving until retired.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS template command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during archival.</param>
        /// <returns>An HTTP 204 No Content response.</returns>
        private static async Task<IResult> HandleDeleteTemplateAsync(
                Guid id,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                DeleteTemplateCommandDto command = new()
                {
                        TemplateId = id,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                await commandFacade.DeleteTemplateAsync(command: command, cancellationToken: cancellationToken);
                return Results.NoContent();
        }
}
