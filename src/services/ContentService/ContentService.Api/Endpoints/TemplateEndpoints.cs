using ContentService.Api.Common;
using ContentService.Api.Contracts;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Templates;
using ContentService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes exhibition template lifecycle endpoints while keeping concurrency, image-reference indexing, and validation encapsulated in Application use cases.
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
                        .Produces<TemplateResult>(StatusCodes.Status201Created)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: HandleListTemplatesAsync)
                        .Produces<PagedResult<TemplateResult>>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapGet(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleGetTemplateAsync)
                        .Produces<TemplateResult>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapPut(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleUpdateTemplateAsync)
                        .Produces<TemplateResult>(StatusCodes.Status200OK)
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
        /// Translates the HTTP creation body into <see cref="CreateTemplateCommand"/> with server-resolved actor identity and returns <c>201 Created</c> with a <c>Location</c> header for immediate follow-up reads.
        /// </summary>
        /// <param name="request">The template creation request payload.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="useCase">The create template application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during persistence.</param>
        /// <returns>An HTTP 201 Created response containing the created <see cref="TemplateResult"/>.</returns>
        private static async Task<IResult> HandleCreateTemplateAsync(
                CreateTemplateRequest request,
                HttpContext httpContext,
                CreateTemplateUseCase useCase,
                CancellationToken cancellationToken)
        {
                // Resolve actor server-side so authenticated claims always override client-supplied ActorId fields.
                CreateTemplateCommand command = new()
                {
                        Name = request.Name,
                        ComponentType = request.ComponentType,
                        SchemaJson = request.SchemaJson,
                        ActorId = RequestActorResolver.ResolveActorId(
                                httpContext: httpContext,
                                explicitActorId: request.ActorId)
                };

                TemplateResult created = await useCase.ExecuteAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Created(
                        uri: $"{ApiConstants.TEMPLATES_ROUTE_PREFIX}/{created.Id}",
                        value: created);
        }

        /// <summary>
        /// Applies default pagination bounds before querying <see cref="ListTemplatesUseCase"/> so unbounded catalog queries cannot overload the API or database.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="componentType">Optional component type filter.</param>
        /// <param name="searchTerm">Optional template name search filter.</param>
        /// <param name="useCase">The list templates application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during listing.</param>
        /// <returns>An HTTP 200 OK response containing a <see cref="PagedResult{T}"/> of templates.</returns>
        private static async Task<IResult> HandleListTemplatesAsync(
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] TemplateComponentType? componentType,
                [FromQuery] string? searchTerm,
                ListTemplatesUseCase useCase,
                CancellationToken cancellationToken)
        {
                ListTemplatesQuery query = new()
                {
                        Page = page ?? ApplicationConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? ApplicationConstants.DEFAULT_PAGE_SIZE,
                        ComponentType = componentType,
                        SearchTerm = searchTerm
                };

                PagedResult<TemplateResult> result = await useCase.ExecuteAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(result);
        }

        /// <summary>
        /// Retrieves a template (including archived ones) by route ID so active drafts and published bundles referencing archived templates continue resolving cleanly.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="useCase">The get template application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during lookup.</param>
        /// <returns>An HTTP 200 OK response containing the template.</returns>
        private static async Task<IResult> HandleGetTemplateAsync(
                Guid id,
                GetTemplateUseCase useCase,
                CancellationToken cancellationToken)
        {
                TemplateResult template = await useCase.ExecuteAsync(
                        templateId: id,
                        cancellationToken: cancellationToken);

                return Results.Ok(template);
        }

        /// <summary>
        /// Binds the route ID and expected concurrency stamp into <see cref="UpdateTemplateCommand"/> so concurrent editor saves fail with <c>409 Conflict</c> instead of silently overwriting changes.
        /// </summary>
        /// <param name="id">The authoritative template identifier from the URL route.</param>
        /// <param name="request">The template update payload with expected concurrency stamp.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="useCase">The update template application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during transactional update.</param>
        /// <returns>An HTTP 200 OK response containing the updated <see cref="TemplateResult"/>.</returns>
        private static async Task<IResult> HandleUpdateTemplateAsync(
                Guid id,
                UpdateTemplateRequest request,
                HttpContext httpContext,
                UpdateTemplateUseCase useCase,
                CancellationToken cancellationToken)
        {
                UpdateTemplateCommand command = new()
                {
                        TemplateId = id,
                        Name = request.Name,
                        SchemaJson = request.SchemaJson,
                        ExpectedConcurrencyStamp = request.ExpectedConcurrencyStamp,
                        ActorId = RequestActorResolver.ResolveActorId(
                                httpContext: httpContext,
                                explicitActorId: request.ActorId)
                };

                TemplateResult updated = await useCase.ExecuteAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(updated);
        }

        /// <summary>
        /// Archives an active template rather than hard-deleting it so existing exhibition drafts keep resolving until retired.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="useCase">The delete template application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during archival.</param>
        /// <returns>An HTTP 204 No Content response.</returns>
        private static async Task<IResult> HandleDeleteTemplateAsync(
                Guid id,
                HttpContext httpContext,
                DeleteTemplateUseCase useCase,
                CancellationToken cancellationToken)
        {
                DeleteTemplateCommand command = new()
                {
                        TemplateId = id,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                await useCase.ExecuteAsync(command: command, cancellationToken: cancellationToken);
                return Results.NoContent();
        }
}
