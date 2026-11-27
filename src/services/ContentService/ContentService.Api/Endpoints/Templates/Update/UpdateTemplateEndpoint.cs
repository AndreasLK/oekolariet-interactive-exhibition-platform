using ContentService.Api.Common;
using ContentService.Api.Contracts;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

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
        /// <returns>An HTTP 200 OK response containing the updated <see cref="TemplateMetadataDto"/>.</returns>
        public static async Task<IResult> HandleAsync(
                Guid id,
                UpdateTemplateRequest request,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Bind the route ID, concurrency stamp, and resolved actor into the update command.
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

                // Persist the updated template and return 200 OK.
                TemplateMetadataDto updated = await commandFacade.UpdateTemplateAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(updated);
        }
}

