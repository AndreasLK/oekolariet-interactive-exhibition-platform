using ContentService.Facade.Common;
using ContentService.Facade.Dtos;
using ContentService.Sdk.Models;

namespace ContentService.Api.Mapper;

/// <summary>
/// Translates between <see cref="ContentService.Sdk.Models"/> template contracts and <see cref="ContentService.Facade.Dtos"/> template commands, queries, and DTOs.
/// </summary>
internal static class TemplateMapper
{
        /// <summary>
        /// Translates an SDK <see cref="ContentCreateTemplateRequest"/> and server-resolved actor into a <see cref="CreateTemplateCommandDto"/>.
        /// </summary>
        /// <param name="request">The incoming SDK template creation request.</param>
        /// <param name="actorId">The server-resolved actor identifier.</param>
        /// <returns>The command DTO ready for <c>ITemplateCommandFacade</c>.</returns>
        public static CreateTemplateCommandDto ToCommand(ContentCreateTemplateRequest request, string actorId)
        {
                // Map the SDK enum and payload into the internal facade creation command.
                return new CreateTemplateCommandDto
                {
                        Name = request.Name,
                        ComponentType = ToFacadeComponentType(componentType: request.ComponentType),
                        SchemaJson = request.SchemaJson,
                        ActorId = actorId
                };
        }

        /// <summary>
        /// Translates an SDK <see cref="ContentUpdateTemplateRequest"/>, route ID, and server-resolved actor into an <see cref="UpdateTemplateCommandDto"/>.
        /// </summary>
        /// <param name="request">The incoming SDK template update request.</param>
        /// <param name="templateId">The authoritative template identifier from the route.</param>
        /// <param name="actorId">The server-resolved actor identifier.</param>
        /// <returns>The command DTO ready for <c>ITemplateCommandFacade</c>.</returns>
        public static UpdateTemplateCommandDto ToCommand(
                ContentUpdateTemplateRequest request,
                Guid templateId,
                string actorId)
        {
                // Bind the authoritative route ID and concurrency stamp into the facade update command.
                return new UpdateTemplateCommandDto
                {
                        TemplateId = templateId,
                        Name = request.Name,
                        SchemaJson = request.SchemaJson,
                        ExpectedConcurrencyStamp = request.ExpectedConcurrencyStamp,
                        ActorId = actorId
                };
        }

        /// <summary>
        /// Converts optional HTTP query parameters into a bounded <see cref="ListTemplatesQueryDto"/> for <c>ITemplateQueryFacade</c>.
        /// </summary>
        /// <param name="page">Optional 1-based page index.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="componentType">Optional SDK component type filter.</param>
        /// <param name="searchTerm">Optional template name search filter.</param>
        /// <returns>A normalized <see cref="ListTemplatesQueryDto"/> with safe pagination defaults.</returns>
        public static ListTemplatesQueryDto ToQuery(
                int? page,
                int? pageSize,
                ContentTemplateComponentType? componentType,
                string? searchTerm)
        {
                // Apply facade pagination defaults and translate the optional component type filter.
                return new ListTemplatesQueryDto
                {
                        Page = page ?? FacadeConstants.DEFAULT_PAGE,
                        PageSize = pageSize ?? FacadeConstants.DEFAULT_PAGE_SIZE,
                        ComponentType = componentType.HasValue
                                ? ToFacadeComponentType(componentType: componentType.Value)
                                : null,
                        SearchTerm = searchTerm
                };
        }

        /// <summary>
        /// Translates a Facade <see cref="TemplateMetadataDto"/> into an SDK <see cref="ContentTemplateResponse"/>.
        /// </summary>
        /// <param name="dto">The template metadata DTO returned by the facade.</param>
        /// <returns>The public SDK <see cref="ContentTemplateResponse"/>.</returns>
        public static ContentTemplateResponse ToResponse(TemplateMetadataDto dto)
        {
                // Project all template fields and map the component type enum to the SDK contract.
                return new ContentTemplateResponse
                {
                        Id = dto.Id,
                        ExternalId = dto.ExternalId,
                        Name = dto.Name,
                        ComponentType = ToSdkComponentType(componentType: dto.ComponentType),
                        SchemaJson = dto.SchemaJson,
                        SchemaVersion = dto.SchemaVersion,
                        IsArchived = dto.IsArchived,
                        ReferencedPictureReferences = dto.ReferencedPictureReferences,
                        CreatedAt = dto.CreatedAt,
                        UpdatedAt = dto.UpdatedAt,
                        ConcurrencyStamp = dto.ConcurrencyStamp
                };
        }

        /// <summary>
        /// Translates a paged Facade <see cref="PagedResultDto{T}"/> of templates into an SDK <see cref="ContentPagedResponse{T}"/>.
        /// </summary>
        /// <param name="pagedDto">The paged facade result.</param>
        /// <returns>A paged SDK response of <see cref="ContentTemplateResponse"/>.</returns>
        public static ContentPagedResponse<ContentTemplateResponse> ToPagedResponse(PagedResultDto<TemplateMetadataDto> pagedDto)
        {
                // Project each template item while preserving pagination metadata.
                List<ContentTemplateResponse> items = new(capacity: pagedDto.Items.Count);
                foreach (TemplateMetadataDto item in pagedDto.Items)
                {
                        items.Add(item: ToResponse(dto: item));
                }

                return new ContentPagedResponse<ContentTemplateResponse>
                {
                        Items = items,
                        Page = pagedDto.Page,
                        PageSize = pagedDto.PageSize,
                        TotalCount = pagedDto.TotalCount
                };
        }

        /// <summary>
        /// Maps an SDK <see cref="ContentTemplateComponentType"/> value to its Facade <see cref="TemplateComponentTypeDto"/> counterpart.
        /// </summary>
        private static TemplateComponentTypeDto ToFacadeComponentType(ContentTemplateComponentType componentType)
        {
                return componentType switch
                {
                        ContentTemplateComponentType.QuizTemplate => TemplateComponentTypeDto.QuizTemplate,
                        ContentTemplateComponentType.GameTemplate => TemplateComponentTypeDto.GameTemplate,
                        ContentTemplateComponentType.InfoScreen => TemplateComponentTypeDto.InfoScreen,
                        ContentTemplateComponentType.Custom => TemplateComponentTypeDto.Custom,
                        _ => throw new ArgumentOutOfRangeException(
                                paramName: nameof(componentType),
                                actualValue: componentType,
                                message: "Unsupported ContentTemplateComponentType value.")
                };
        }

        /// <summary>
        /// Maps a Facade <see cref="TemplateComponentTypeDto"/> value to its SDK <see cref="ContentTemplateComponentType"/> counterpart.
        /// </summary>
        private static ContentTemplateComponentType ToSdkComponentType(TemplateComponentTypeDto componentType)
        {
                return componentType switch
                {
                        TemplateComponentTypeDto.QuizTemplate => ContentTemplateComponentType.QuizTemplate,
                        TemplateComponentTypeDto.GameTemplate => ContentTemplateComponentType.GameTemplate,
                        TemplateComponentTypeDto.InfoScreen => ContentTemplateComponentType.InfoScreen,
                        TemplateComponentTypeDto.Custom => ContentTemplateComponentType.Custom,
                        _ => throw new ArgumentOutOfRangeException(
                                paramName: nameof(componentType),
                                actualValue: componentType,
                                message: "Unsupported TemplateComponentTypeDto value.")
                };
        }
}

