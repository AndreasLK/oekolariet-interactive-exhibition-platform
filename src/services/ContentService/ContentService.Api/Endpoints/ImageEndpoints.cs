using ContentService.Api.Common;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Images;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes HTTP endpoints for exhibition image uploads, catalog queries, binary downloads, and soft-deletion while delegating all domain/quota rules to Application use cases.
/// </summary>
internal static class ImageEndpoints
{
        /// <summary>
        /// Wires the <c>/api/images</c> route group with explicit OpenAPI contracts and stateless upload settings for cross-origin Blazor and service callers.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated route group builder for further endpoint metadata configuration.</returns>
        public static RouteGroupBuilder MapImageEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                RouteGroupBuilder group = endpoints.MapGroup(ApiConstants.IMAGES_ROUTE_PREFIX)
                        .WithTags(ApiConstants.IMAGES_OPENAPI_TAG);

                // Disable cookie antiforgery on the stateless API upload endpoint so Blazor WASM and backend clients can post multipart files without browser form tokens.
                group.MapPost(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: HandleUploadImageAsync)
                        .DisableAntiforgery()
                        .Produces<ImageMetadataResult>(StatusCodes.Status201Created)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
                        .ProducesProblem(StatusCodes.Status507InsufficientStorage);

                group.MapGet(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: HandleListImagesAsync)
                        .Produces<PagedResult<ImageMetadataResult>>(StatusCodes.Status200OK);

                group.MapGet(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleGetImageMetadataAsync)
                        .Produces<ImageMetadataResult>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: ApiConstants.IMAGE_CONTENT_ROUTE, handler: HandleGetImageContentAsync)
                        .Produces(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapDelete(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleDeleteImageAsync)
                        .Produces(StatusCodes.Status204NoContent)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                return group;
        }

        /// <summary>
        /// Forwards the raw multipart upload stream directly to <see cref="UploadImageUseCase"/> so large files are not double-buffered at the API boundary before size and quota checks run.
        /// </summary>
        /// <param name="file">The uploaded multipart image file.</param>
        /// <param name="altText">Optional accessibility alt text.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="useCase">The upload image application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects to abort upload streaming and persistence.</param>
        /// <returns>An HTTP 201 Created response containing the persisted <see cref="ImageMetadataResult"/>.</returns>
        private static async Task<IResult> HandleUploadImageAsync(
                IFormFile file,
                [FromForm] string? altText,
                HttpContext httpContext,
                UploadImageUseCase useCase,
                CancellationToken cancellationToken)
        {
                // Pass the multipart stream directly instead of buffering a second copy in the API layer; UploadImageUseCase enforces strict byte ceilings as it reads.
                await using Stream contentStream = file.OpenReadStream();
                UploadImageCommand command = new()
                {
                        FileName = file.FileName ?? string.Empty,
                        ContentType = ExtractMediaType(rawContentType: file.ContentType),
                        DeclaredLengthBytes = file.Length,
                        Content = contentStream,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext),
                        AltText = altText
                };

                ImageMetadataResult created = await useCase.ExecuteAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Created(
                        uri: $"{ApiConstants.IMAGES_ROUTE_PREFIX}/{created.Id}",
                        value: created);
        }

        /// <summary>
        /// Applies default pagination bounds before querying <see cref="ListImagesUseCase"/> so catalog browsing cannot trigger unbounded database reads.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="searchTerm">Optional filename search term.</param>
        /// <param name="useCase">The list images application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during listing.</param>
        /// <returns>An HTTP 200 OK response containing a <see cref="PagedResult{T}"/> of image metadata.</returns>
        private static async Task<IResult> HandleListImagesAsync(
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] string? searchTerm,
                ListImagesUseCase useCase,
                CancellationToken cancellationToken)
        {
                ListImagesQuery query = new()
                {
                        Page = page ?? ApplicationConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? ApplicationConstants.DEFAULT_PAGE_SIZE,
                        SearchTerm = searchTerm
                };

                PagedResult<ImageMetadataResult> result = await useCase.ExecuteAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(result);
        }

        /// <summary>
        /// Exposes image metadata independently of <c>/content</c> so editors and publishers can inspect file size, alt text, and SHA-256 digests without transferring image bytes.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="useCase">The get image metadata application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during lookup.</param>
        /// <returns>An HTTP 200 OK response containing the image metadata.</returns>
        private static async Task<IResult> HandleGetImageMetadataAsync(
                Guid id,
                GetImageMetadataUseCase useCase,
                CancellationToken cancellationToken)
        {
                ImageMetadataResult metadata = await useCase.ExecuteAsync(
                        imageId: id,
                        cancellationToken: cancellationToken);

                return Results.Ok(metadata);
        }

        /// <summary>
        /// Downloads raw image bytes with immutable caching and security headers, transferring stream disposal ownership to ASP.NET Core.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="httpContext">The active HTTP context used to set response headers.</param>
        /// <param name="useCase">The get image stream application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects to cancel loading.</param>
        /// <returns>An HTTP 200 streaming response whose stream ownership transfers to ASP.NET Core.</returns>
        private static async Task<IResult> HandleGetImageContentAsync(
                Guid id,
                HttpContext httpContext,
                GetImageStreamUseCase useCase,
                CancellationToken cancellationToken)
        {
                ImageStreamResult streamResult = await useCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = id },
                        cancellationToken: cancellationToken);

                // Dispose streamResult if header setup fails before Results.Stream assumes ownership.
                try
                {
                        httpContext.Response.Headers.CacheControl = ApiConstants.IMAGE_CACHE_CONTROL_HEADER_VALUE;
                        ConfigureContentResponseHeaders(response: httpContext.Response, metadata: streamResult.Metadata);
                        return Results.Stream(
                                stream: streamResult.Content,
                                contentType: streamResult.Metadata.ContentType);
                }
                catch
                {
                        await streamResult.DisposeAsync();
                        throw;
                }
        }

        /// <summary>
        /// Soft-deletes an unreferenced image so storage quota is reclaimed while active template links remain protected by <c>409 Conflict</c>.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="useCase">The delete image application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects during deletion.</param>
        /// <returns>An HTTP 204 No Content response.</returns>
        private static async Task<IResult> HandleDeleteImageAsync(
                Guid id,
                HttpContext httpContext,
                DeleteImageUseCase useCase,
                CancellationToken cancellationToken)
        {
                DeleteImageCommand command = new()
                {
                        ImageId = id,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                await useCase.ExecuteAsync(command: command, cancellationToken: cancellationToken);
                return Results.NoContent();
        }

        /// <summary>
        /// Strips optional MIME parameters (such as <c>; charset=utf-8</c>) so valid uploads are not falsely rejected by exact allowlist comparisons.
        /// </summary>
        /// <param name="rawContentType">The raw multipart <c>Content-Type</c> header value.</param>
        /// <returns>The bare media type string, or the raw value when unparseable so downstream validation reports the bad header.</returns>
        private static string ExtractMediaType(string? rawContentType)
        {
                if (string.IsNullOrWhiteSpace(rawContentType))
                {
                        return string.Empty;
                }

                return MediaTypeHeaderValue.TryParse(input: rawContentType, parsedValue: out MediaTypeHeaderValue? parsed)
                        ? parsed.MediaType.Value ?? string.Empty
                        : rawContentType;
        }

        /// <summary>
        /// Sets content length, range-support notice, MIME-sniffing protection, and UTF-8 safe inline disposition so browsers render images safely and accurately.
        /// </summary>
        /// <param name="response">The outgoing HTTP response.</param>
        /// <param name="metadata">The image metadata describing size and file name.</param>
        private static void ConfigureContentResponseHeaders(HttpResponse response, ImageMetadataResult metadata)
        {
                response.ContentLength = metadata.SizeBytes;
                response.Headers[HeaderNames.AcceptRanges] = ApiConstants.ACCEPT_RANGES_NONE;
                response.Headers[HeaderNames.XContentTypeOptions] = ApiConstants.CONTENT_TYPE_OPTIONS_NOSNIFF;

                // Use FileNameStar so RFC 6266 UTF-8 percent-encoding preserves spaces and Danish/non-ASCII characters safely in HTTP headers.
                ContentDispositionHeaderValue disposition = new(ApiConstants.INLINE_DISPOSITION_TYPE)
                {
                        FileNameStar = metadata.FileName
                };
                response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        }
}
