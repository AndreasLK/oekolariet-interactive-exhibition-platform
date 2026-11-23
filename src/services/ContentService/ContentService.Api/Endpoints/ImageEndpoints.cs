using ContentService.Api.Common;
using ContentService.Api.Contracts;
using ContentService.Facade.Common;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes HTTP endpoints for exhibition image uploads, catalog queries, binary downloads, and soft-deletion while delegating all domain/quota rules to CQS facades.
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
                        .Produces<ImageMetadataDto>(StatusCodes.Status201Created)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
                        .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
                        .ProducesProblem(StatusCodes.Status507InsufficientStorage);

                group.MapGet(pattern: ApiConstants.GROUP_ROOT_ROUTE, handler: HandleListImagesAsync)
                        .Produces<PagedResultDto<ImageMetadataDto>>(StatusCodes.Status200OK);

                group.MapGet(pattern: ApiConstants.IMAGE_ALBUMS_ROUTE, handler: HandleListAlbumsAsync)
                        .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest);

                group.MapGet(pattern: ApiConstants.RESOURCE_ID_ROUTE, handler: HandleGetImageMetadataAsync)
                        .Produces<ImageMetadataDto>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapPatch(pattern: ApiConstants.IMAGE_METADATA_ROUTE, handler: HandleUpdateImageMetadataAsync)
                        .Produces<ImageMetadataDto>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound)
                        .ProducesProblem(StatusCodes.Status409Conflict);

                group.MapGet(pattern: ApiConstants.IMAGE_CONTENT_ROUTE, handler: HandleGetImageContentAsync)
                        .Produces(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                group.MapGet(pattern: ApiConstants.IMAGE_PREVIEW_ROUTE, handler: HandleGetImagePreviewAsync)
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
        /// Forwards the raw multipart upload stream directly to <see cref="IImageCommandFacade"/> so large files are not double-buffered at the API boundary before size and quota checks run.
        /// </summary>
        /// <param name="file">The uploaded multipart image file.</param>
        /// <param name="altText">Optional accessibility alt text / description.</param>
        /// <param name="albumPath">Optional recursive album path (e.g., <c>Madaffald/Frugt</c>).</param>
        /// <param name="tags">Optional comma-separated search tags.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS image command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects to abort upload streaming and persistence.</param>
        /// <returns>An HTTP 201 Created response containing the persisted <see cref="ImageMetadataDto"/>.</returns>
        private static async Task<IResult> HandleUploadImageAsync(
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

                return Results.Created(
                        uri: $"{ApiConstants.IMAGES_ROUTE_PREFIX}/{created.Id}",
                        value: created);
        }

        /// <summary>
        /// Applies default pagination bounds before querying <see cref="IImageQueryFacade"/> so catalog browsing cannot trigger unbounded database reads.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="searchTerm">Optional search filter matched against file name, alt text, and tags.</param>
        /// <param name="albumPath">Optional recursive album path filter.</param>
        /// <param name="includeSubalbums">Optional flag controlling whether nested sub-albums are included (defaults to true).</param>
        /// <param name="queryFacade">The CQS image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during listing.</param>
        /// <returns>An HTTP 200 OK response containing a <see cref="PagedResultDto{T}"/> of image metadata.</returns>
        private static async Task<IResult> HandleListImagesAsync(
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] string? searchTerm,
                [FromQuery] string? albumPath,
                [FromQuery] bool? includeSubalbums,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                ListImagesQueryDto query = new()
                {
                        Page = page ?? FacadeConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? FacadeConstants.DEFAULT_PAGE_SIZE,
                        SearchTerm = searchTerm,
                        AlbumPath = albumPath,
                        IncludeSubalbums = includeSubalbums ?? true
                };

                PagedResultDto<ImageMetadataDto> result = await queryFacade.ListImagesAsync(
                        query: query,
                        cancellationToken: cancellationToken);

                return Results.Ok(result);
        }

        /// <summary>
        /// Lists immediate child album paths directly beneath an optional parent album.
        /// </summary>
        /// <param name="parentPath">Optional parent album path, or <see langword="null"/> for root albums.</param>
        /// <param name="queryFacade">The CQS image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during album lookup.</param>
        /// <returns>An HTTP 200 OK response containing sorted immediate child album paths.</returns>
        private static async Task<IResult> HandleListAlbumsAsync(
                [FromQuery] string? parentPath,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                IReadOnlyList<string> childAlbums = await queryFacade.ListAlbumsAsync(
                        parentAlbumPath: parentPath,
                        cancellationToken: cancellationToken);

                return Results.Ok(childAlbums);
        }

        /// <summary>
        /// Updates searchable description (<c>AltText</c>), tags, and album path for an active image while enforcing optimistic concurrency.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="request">The metadata update payload and expected concurrency stamp.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS image command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during persistence.</param>
        /// <returns>An HTTP 200 OK response containing the updated <see cref="ImageMetadataDto"/>.</returns>
        private static async Task<IResult> HandleUpdateImageMetadataAsync(
                Guid id,
                UpdateImageMetadataRequest request,
                HttpContext httpContext,
                IImageCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                UpdateImageMetadataCommandDto command = new()
                {
                        ImageId = id,
                        AltText = request.AltText,
                        Tags = request.Tags,
                        AlbumPath = request.AlbumPath,
                        ExpectedConcurrencyStamp = request.ExpectedConcurrencyStamp,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                ImageMetadataDto updated = await commandFacade.UpdateImageMetadataAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(updated);
        }

        /// <summary>
        /// Exposes image metadata independently of <c>/content</c> so editors and publishers can inspect file size, alt text, and SHA-256 digests without transferring image bytes.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="queryFacade">The CQS image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during lookup.</param>
        /// <returns>An HTTP 200 OK response containing the image metadata.</returns>
        private static async Task<IResult> HandleGetImageMetadataAsync(
                Guid id,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                ImageMetadataDto metadata = await queryFacade.GetImageMetadataAsync(
                        imageId: id,
                        cancellationToken: cancellationToken);

                return Results.Ok(metadata);
        }

        /// <summary>
        /// Downloads raw image bytes with immutable caching and security headers, transferring stream disposal ownership to ASP.NET Core.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="httpContext">The active HTTP context used to set response headers.</param>
        /// <param name="queryFacade">The CQS image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects to cancel loading.</param>
        /// <returns>An HTTP 200 streaming response whose stream ownership transfers to ASP.NET Core.</returns>
        private static async Task<IResult> HandleGetImageContentAsync(
                Guid id,
                HttpContext httpContext,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                ImageStreamDto streamDto = await queryFacade.GetImageContentAsync(
                        imageId: id,
                        cancellationToken: cancellationToken);

                return await StreamImageAsync(streamDto: streamDto, httpContext: httpContext);
        }

        /// <summary>
        /// Streams the saved lightweight preview image with immutable caching headers, generating and persisting it on first access if absent.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="httpContext">The active HTTP context used to set response headers.</param>
        /// <param name="queryFacade">The CQS image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects to cancel loading or generation.</param>
        /// <returns>An HTTP 200 streaming response whose stream ownership transfers to ASP.NET Core.</returns>
        private static async Task<IResult> HandleGetImagePreviewAsync(
                Guid id,
                HttpContext httpContext,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                ImageStreamDto streamDto = await queryFacade.GetImagePreviewAsync(
                        imageId: id,
                        cancellationToken: cancellationToken);

                return await StreamImageAsync(streamDto: streamDto, httpContext: httpContext);
        }

        /// <summary>
        /// Soft-deletes an unreferenced image so storage quota is reclaimed while active template links remain protected by <c>409 Conflict</c>.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="httpContext">The active HTTP context used to resolve trusted actor identity.</param>
        /// <param name="commandFacade">The CQS image command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects during deletion.</param>
        /// <returns>An HTTP 204 No Content response.</returns>
        private static async Task<IResult> HandleDeleteImageAsync(
                Guid id,
                HttpContext httpContext,
                IImageCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                DeleteImageCommandDto command = new()
                {
                        ImageId = id,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                await commandFacade.DeleteImageAsync(command: command, cancellationToken: cancellationToken);
                return Results.NoContent();
        }

        /// <summary>
        /// Configures caching/security response headers and hands stream disposal ownership to ASP.NET Core, cleaning up on header failure.
        /// </summary>
        /// <param name="streamDto">The open image stream DTO.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <returns>An HTTP 200 streaming <see cref="IResult"/>.</returns>
        private static async Task<IResult> StreamImageAsync(
                ImageStreamDto streamDto,
                HttpContext httpContext)
        {
                // Dispose streamDto if header setup fails before Results.Stream assumes ownership.
                try
                {
                        ImageContentResponseHeaders.Configure(response: httpContext.Response, metadata: streamDto.Metadata);
                        return Results.Stream(
                                stream: streamDto.Content,
                                contentType: streamDto.Metadata.ContentType);
                }
                catch
                {
                        await streamDto.DisposeAsync();
                        throw;
                }
        }
}
