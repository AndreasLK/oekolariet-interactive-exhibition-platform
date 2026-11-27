using ContentService.Api.Common;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles multipart exhibition image uploads (<c>POST /api/v1/images</c>).
/// </summary>
internal static class UploadImageEndpoint
{
        /// <summary>
        /// Forwards the raw multipart upload stream directly to <see cref="IImageCommandFacade"/> so large files are not double-buffered at the API boundary before size and quota checks run.
        /// </summary>
        /// <param name="file">The uploaded multipart form file.</param>
        /// <param name="altText">Optional accessibility description.</param>
        /// <param name="albumPath">Optional hierarchical album path.</param>
        /// <param name="tags">Optional comma-separated tags.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <param name="commandFacade">The image command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 201 Created response containing the created <see cref="ImageMetadataDto"/>.</returns>
        public static async Task<IResult> HandleAsync(
                IFormFile file,
                [FromForm] string? altText,
                [FromForm] string? albumPath,
                [FromForm] string? tags,
                HttpContext httpContext,
                IImageCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Pass the multipart stream directly instead of buffering a second copy in the API layer; downstream validation enforces strict byte ceilings as it reads.
                await using Stream contentStream = file.OpenReadStream();
                UploadImageCommandDto command = new()
                {
                        FileName = file.FileName ?? string.Empty,
                        ContentType = ImageContentResponseHeaders.ExtractMediaType(rawContentType: file.ContentType),
                        DeclaredLengthBytes = file.Length,
                        Content = contentStream,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext),
                        AltText = altText,
                        AlbumPath = albumPath,
                        Tags = tags
                };

                ImageMetadataDto created = await commandFacade.UploadImageAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                // Emit the concrete v1 path with the shard-aware ExternalId ({node}_{guid}) so follow-up GETs route to the owning node.
                return Results.Created(
                        uri: $"{ApiConstants.V1_IMAGES_ROUTE_PREFIX}/{created.ExternalId}",
                        value: created);
        }
}

