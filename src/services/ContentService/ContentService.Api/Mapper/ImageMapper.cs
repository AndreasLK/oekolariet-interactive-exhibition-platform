using ContentService.Facade.Common;
using ContentService.Facade.Dtos;
using ContentService.Sdk.Models;

namespace ContentService.Api.Mapper;

/// <summary>
/// Translates between <see cref="ContentService.Sdk.Models"/> image contracts and <see cref="ContentService.Facade.Dtos"/> image commands, queries, and DTOs.
/// </summary>
internal static class ImageMapper
{
        /// <summary>
        /// Converts optional HTTP query parameters into a bounded <see cref="ListImagesQueryDto"/> for <c>IImageQueryFacade</c>.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="searchTerm">Optional search filter.</param>
        /// <param name="albumPath">Optional album path filter.</param>
        /// <param name="includeSubalbums">Optional flag controlling recursive sub-album inclusion.</param>
        /// <returns>A normalized <see cref="ListImagesQueryDto"/> with safe pagination defaults.</returns>
        public static ListImagesQueryDto ToQuery(
                int? page,
                int? pageSize,
                string? searchTerm,
                string? albumPath,
                bool? includeSubalbums)
        {
                // Apply facade pagination defaults when query parameters are omitted by the caller.
                return new ListImagesQueryDto
                {
                        Page = page ?? FacadeConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? FacadeConstants.DEFAULT_PAGE_SIZE,
                        SearchTerm = searchTerm,
                        AlbumPath = albumPath,
                        IncludeSubalbums = includeSubalbums ?? true
                };
        }

        /// <summary>
        /// Translates an SDK <see cref="ContentUpdateImageMetadataRequest"/> and route context into an <see cref="UpdateImageMetadataCommandDto"/>.
        /// </summary>
        /// <param name="request">The incoming SDK metadata patch request.</param>
        /// <param name="imageId">The parsed target image identifier.</param>
        /// <param name="storageNodeId">The optional target storage node identifier.</param>
        /// <param name="allowPeerFallback">Whether peer forwarding is permitted for this request.</param>
        /// <param name="actorId">The server-resolved actor identifier.</param>
        /// <returns>The command DTO ready for <c>IImageCommandFacade</c>.</returns>
        public static UpdateImageMetadataCommandDto ToCommand(
                ContentUpdateImageMetadataRequest request,
                Guid imageId,
                string? storageNodeId,
                bool allowPeerFallback,
                string actorId)
        {
                // Bind route targeting and trusted actor identity alongside the SDK patch fields.
                return new UpdateImageMetadataCommandDto
                {
                        ImageId = imageId,
                        StorageNodeId = storageNodeId,
                        AllowPeerFallback = allowPeerFallback,
                        AltText = request.AltText,
                        Tags = request.Tags,
                        AlbumPath = request.AlbumPath,
                        ExpectedConcurrencyStamp = request.ExpectedConcurrencyStamp,
                        ActorId = actorId
                };
        }

        /// <summary>
        /// Translates a Facade <see cref="ImageMetadataDto"/> into an SDK <see cref="ContentImageResponse"/>.
        /// </summary>
        /// <param name="dto">The image metadata DTO returned by the facade.</param>
        /// <returns>The public SDK <see cref="ContentImageResponse"/>.</returns>
        public static ContentImageResponse ToResponse(ImageMetadataDto dto)
        {
                // Project all persisted image metadata fields to the shared SDK response contract.
                return new ContentImageResponse
                {
                        Id = dto.Id,
                        PictureReference = dto.PictureReference,
                        ExternalId = dto.ExternalId,
                        FileName = dto.FileName,
                        ContentType = dto.ContentType,
                        SizeBytes = dto.SizeBytes,
                        Sha256Hash = dto.Sha256Hash,
                        AltText = dto.AltText,
                        AlbumPath = dto.AlbumPath,
                        Tags = dto.Tags,
                        CreatedAt = dto.CreatedAt,
                        ConcurrencyStamp = dto.ConcurrencyStamp
                };
        }

        /// <summary>
        /// Translates a batch of Facade <see cref="ImageMetadataDto"/> items into SDK <see cref="ContentImageResponse"/> items.
        /// </summary>
        /// <param name="dtos">The image metadata DTOs returned by the facade.</param>
        /// <returns>A read-only list of SDK <see cref="ContentImageResponse"/> items.</returns>
        public static IReadOnlyList<ContentImageResponse> ToResponseList(IReadOnlyList<ImageMetadataDto> dtos)
        {
                // Project each facade DTO while preserving order.
                List<ContentImageResponse> responses = new(capacity: dtos.Count);
                foreach (ImageMetadataDto dto in dtos)
                {
                        responses.Add(item: ToResponse(dto: dto));
                }

                return responses;
        }

        /// <summary>
        /// Translates a paged Facade <see cref="PagedResultDto{T}"/> of images into an SDK <see cref="ContentPagedResponse{T}"/>.
        /// </summary>
        /// <param name="pagedDto">The paged facade result.</param>
        /// <returns>A paged SDK response of <see cref="ContentImageResponse"/>.</returns>
        public static ContentPagedResponse<ContentImageResponse> ToPagedResponse(PagedResultDto<ImageMetadataDto> pagedDto)
        {
                // Preserve pagination metadata while mapping each item to its SDK response type.
                return new ContentPagedResponse<ContentImageResponse>
                {
                        Items = ToResponseList(dtos: pagedDto.Items),
                        Page = pagedDto.Page,
                        PageSize = pagedDto.PageSize,
                        TotalCount = pagedDto.TotalCount
                };
        }
}

