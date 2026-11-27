using ContentService.Api.Common;
using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;

namespace ContentService.Api.Endpoints.Templates;

/// <summary>
/// Handles exhibition template updates (<c>PUT /api/v1/templates/{id}</c>).
/// </summary>
internal static class UpdateTemplateEndpoint
{
        /// <summary>
        /// Binds the route ID and expected concurrency stamp into <see cref="UpdateTemplateCommandDto"/> so concurrent editor saves fail with <c>409 Conflict</c> instead of silently overwriting changes.
        /// </summary>
        /// <param name="id">The authoritative template identifier from the URL route.</param>
        /// <param name="request">The template update payload with expected concurrency stamp.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS template command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during transactional update.</param>
        /// <returns>An HTTP 200 OK response containing the updated <see cref="ContentTemplateResponse"/>.</returns>
        public static async Task<IResult> HandleAsync(
                Guid id,
                ContentUpdateTemplateRequest request,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Bind the route ID, concurrency stamp, and resolved actor into the update command.
                string actorId = RequestActorResolver.ResolveActorId(
                        httpContext: httpContext,
                        explicitActorId: request.ActorId);

                UpdateTemplateCommandDto command = TemplateMapper.ToCommand(
                        request: request,
                        templateId: id,
                        actorId: actorId);

                // Persist the updated template and return the mapped SDK response.
                TemplateMetadataDto updated = await commandFacade.UpdateTemplateAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(value: TemplateMapper.ToResponse(dto: updated));
        }
}

