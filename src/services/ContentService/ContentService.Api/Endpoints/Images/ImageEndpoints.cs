using ContentService.Api.Common;
using ContentService.Api.Endpoints.Images;
using ContentService.Sdk.Models;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Wires the <c>/api/v1/images</c> route group to individual image query, stream, and command endpoint handlers.
/// </summary>
internal static class ImageEndpoints
{
        /// <summary>
        /// Maps the <c>/api/v1/images</c> route group with explicit OpenAPI contracts and stateless upload settings for cross-origin Blazor and service callers.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated route group builder for further endpoint metadata configuration.</returns>
        public static RouteGroupBuilder MapImageEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                RouteGroupBuilder group = endpoints.MapGroup(ApiConstants.IMAGES_GROUP_RELATIVE_ROUTE)
                        .WithTags(ApiConstants.IMAGES_OPENAPI_TAG);

                // Disable cookie antiforgery on the stateless API upload endpoint so Blazor WASM and backend clients can post multipart files without browser form tokens.
                group.MapPost(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: UploadImageEndpoint.HandleAsync)
                        .DisableAntiforgery()
                        .Produces<ContentImageResponse>(StatusCodes.Status201Created)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
                        .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
                        .ProducesProblem(StatusCodes.Status507InsufficientStorage);

                group.MapGet(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: ListImagesEndpoint.HandleAsync)
                        .Produces<ContentPagedResponse<ContentImageResponse>>(StatusCodes.Status200OK);

                group.MapGet(pattern: ApiConstants.IMAGE_ALBUMS_ROUTE, handler: ListAlbumsEndpoint.HandleAsync)
                        .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapPost(pattern: ApiConstants.IMAGE_BATCH_METADATA_ROUTE, handler: GetImagesMetadataBatchEndpoint.HandleAsync)
                        .Produces<IReadOnlyList<ContentImageResponse>>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapGet(pattern: ApiConstants.IMAGE_IDENTIFIER_ROUTE, handler: GetImageMetadataEndpoint.HandleAsync)
                        .Produces<ContentImageResponse>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapPatch(pattern: ApiConstants.IMAGE_METADATA_ROUTE, handler: UpdateImageMetadataEndpoint.HandleAsync)
                        .Produces<ContentImageResponse>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                group.MapGet(pattern: ApiConstants.IMAGE_CONTENT_ROUTE, handler: GetImageContentEndpoint.HandleAsync)
                        .Produces(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: ApiConstants.IMAGE_PREVIEW_ROUTE, handler: GetImagePreviewEndpoint.HandleAsync)
                        .Produces(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapDelete(pattern: ApiConstants.IMAGE_IDENTIFIER_ROUTE, handler: DeleteImageEndpoint.HandleAsync)
                        .Produces(StatusCodes.Status204NoContent)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                return group;
        }
}

