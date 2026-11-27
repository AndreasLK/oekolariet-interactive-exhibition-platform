using ContentService.Api.Common;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

namespace ContentService.Api.Endpoints.Templates;

/// <summary>
/// Handles exhibition template archival (<c>DELETE /api/v1/templates/{id}</c>).
/// </summary>
internal static class DeleteTemplateEndpoint
{
        /// <summary>
        /// Archives an active template rather than hard-deleting it so existing exhibition drafts keep resolving until retired.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS template command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during archival.</param>
        /// <returns>An HTTP 204 No Content response.</returns>
        public static async Task<IResult> HandleAsync(
                Guid id,
                HttpContext httpContext,
                ITemplateCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Build the archival command with the resolved actor ID for audit tracking.
                DeleteTemplateCommandDto command = new()
                {
                        TemplateId = id,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                // Archive the template and return 204 No Content.
                await commandFacade.DeleteTemplateAsync(command: command, cancellationToken: cancellationToken);
                return Results.NoContent();
        }
}

