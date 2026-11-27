using ContentService.Api.Common;
using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;

namespace ContentService.Api.Endpoints.Templates;

/// <summary>
/// Handles exhibition template creation (<c>POST /api/v1/templates</c>).
/// </summary>
internal static class CreateTemplateEndpoint
{
        /// <summary>
        /// Translates the HTTP creation body into <see cref="CreateTemplateCommandDto"/> with server-resolved actor identity and returns <c>201 Created</c> with a <c>Location</c> header for immediate follow-up reads.
        /// </summary>
        /// <param name="request">The template creation request payload.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS template command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during persistence.</param>
        /// <returns>An HTTP 201 Created response containing the created <see cref="ContentTemplateResponse"/>.</returns>
        public static async Task<IResult> HandleAsync(
                ContentCreateTemplateRequest request,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Resolve actor server-side so authenticated claims always override client-supplied ActorId fields.
                string actorId = RequestActorResolver.ResolveActorId(
                        httpContext: httpContext,
                        explicitActorId: request.ActorId);

                CreateTemplateCommandDto command = TemplateMapper.ToCommand(
                        request: request,
                        actorId: actorId);

                TemplateMetadataDto created = await commandFacade.CreateTemplateAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                // Emit the concrete v1 path rather than a route template so clients can follow the Location header directly.
                return Results.Created(
                        uri: $"{ApiConstants.V1_TEMPLATES_ROUTE_PREFIX}/{created.Id}",
                        value: TemplateMapper.ToResponse(dto: created));
        }
}

