using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

namespace ContentService.Api.Endpoints.Pool;

/// <summary>
/// Receives direct Dapr scatter-gather replies from peer nodes (<c>POST /api/v1/pool/replies/{correlationId}</c>).
/// </summary>
internal static class PoolReplyEndpoint
{
        /// <summary>
        /// Completes the waiting scatter-gather request associated with <paramref name="correlationId"/>.
        /// </summary>
        /// <param name="correlationId">The correlation identifier of the pending scatter-gather request.</param>
        /// <param name="offer">The peer node's offer payload.</param>
        /// <param name="poolFacade">The pool facade managing pending requests.</param>
        /// <returns>An HTTP 200 OK response.</returns>
        public static IResult Handle(
                Guid correlationId,
                PoolNodeOfferDto offer,
                IPoolFacade poolFacade)
        {
                // Complete the awaiting scatter-gather request with the peer's offer DTO.
                poolFacade.CompletePendingRequest(correlationId: correlationId, offer: offer);
                return Results.Ok();
        }
}

