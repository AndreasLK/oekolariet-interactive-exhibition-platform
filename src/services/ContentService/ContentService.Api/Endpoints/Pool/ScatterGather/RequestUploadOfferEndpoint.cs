using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;

namespace ContentService.Api.Endpoints.Pool;

/// <summary>
/// Broadcasts upload-capacity requests across the storage pool (<c>POST /api/v1/pool/upload-offers</c>).
/// </summary>
internal static class RequestUploadOfferEndpoint
{
        /// <summary>
        /// Broadcasts an upload-capacity request across all subscribed nodes and returns the first healthy node's offer (or 503 after timeout).
        /// </summary>
        /// <param name="request">The upload offer request specifying required bytes.</param>
        /// <param name="poolFacade">The pool facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK with <see cref="ContentPoolNodeOfferResponse"/> or 503 Service Unavailable.</returns>
        public static async Task<IResult> HandleAsync(
                ContentRequestUploadOfferRequest request,
                IPoolFacade poolFacade,
                CancellationToken cancellationToken)
        {
                // Broadcast the required byte capacity to the pool and await the first healthy offer.
                PoolNodeOfferDto? offer = await poolFacade.RequestHealthyUploadOfferAsync(
                        requiredBytes: request.RequiredBytes,
                        cancellationToken: cancellationToken);

                // Return 200 OK with the mapped SDK offer, or 503 if no node responded with enough capacity.
                return offer is not null
                        ? Results.Ok(value: PlatformMapper.ToResponse(dto: offer))
                        : Results.Problem(
                                statusCode: StatusCodes.Status503ServiceUnavailable,
                                title: "No healthy storage node available.",
                                detail: "No ContentService node offered sufficient healthy capacity within the pool timeout.");
        }
}

