using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Templates;

/// <summary>
/// Creates a new exhibition template after validating JSON structure, unique naming, and referenced image existence.
/// </summary>
public sealed class CreateTemplateUseCase
{
        /// <summary>
        /// Template aggregate repository.
        /// </summary>
        private readonly ITemplateRepository _templateRepository;

        /// <summary>
        /// Image metadata repository used to verify referenced images.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Configured storage options supplying the local storage node identifier.
        /// </summary>
        private readonly ContentStorageOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateTemplateUseCase"/> class.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="options">The configured content storage options.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public CreateTemplateUseCase(
                ITemplateRepository templateRepository,
                IImageRepository imageRepository,
                IOptions<ContentStorageOptions> options)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(options);
                ArgumentNullException.ThrowIfNull(options.Value);

                this._templateRepository = templateRepository;
                this._imageRepository = imageRepository;
                this._options = options.Value;
        }

        /// <summary>
        /// Validates template inputs, resolves referenced images, and persists a new <see cref="TemplateEntity"/>.
        /// </summary>
        /// <param name="command">The template creation command.</param>
        /// <param name="cancellationToken">A token to observe during repository checks and persistence.</param>
        /// <returns>The created <see cref="TemplateResult"/> including indexed picture references.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when component type, name, actor, JSON syntax, base64 check, or uniqueness fails.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when any referenced <c>picture.{node}.{guid}</c> reference does not exist.</exception>
        public async Task<TemplateResult> ExecuteAsync(
                CreateTemplateCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);
                ValidateCommandInputs(command: command);

                string trimmedName = command.Name.Trim();
                bool duplicateExists = await this._templateRepository.ExistsByNameAsync(
                        name: trimmedName,
                        excludeTemplateId: null,
                        cancellationToken: cancellationToken);
                if (duplicateExists)
                {
                        throw new DomainValidationException($"A template named '{trimmedName}' already exists.");
                }

                // Reuse PictureReference.ExtractReferences so base64 smuggling checks and regex matching stay strictly in Domain.
                List<PictureReference> referencedPictures = PictureReference.ExtractReferences(jsonPayload: command.SchemaJson);
                await ReferencedImageVerifier.VerifyAndResolveAsync(
                        imageRepository: this._imageRepository,
                        references: referencedPictures,
                        cancellationToken: cancellationToken);

                // TemplateEntity.Create trims and normalizes Name internally; pass trimmedName to ExistsByNameAsync for an exact duplicate check.
                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: this._options.StorageNodeId,
                        name: command.Name,
                        componentType: command.ComponentType,
                        schemaJson: command.SchemaJson,
                        actorId: command.ActorId);

                await this._templateRepository.AddAsync(
                        template: template,
                        cancellationToken: cancellationToken);

                return ContentResultMapper.ToTemplateResult(template: template);
        }

        /// <summary>
        /// Validates enum bounds, string lengths, and JSON object syntax before performing repository queries.
        /// </summary>
        /// <param name="command">The command payload to validate.</param>
        /// <exception cref="DomainValidationException">Thrown when any input invariant fails.</exception>
        private static void ValidateCommandInputs(CreateTemplateCommand command)
        {
                if (!Enum.IsDefined(command.ComponentType))
                {
                        throw new DomainValidationException(
                                $"Template component type '{command.ComponentType}' is not recognized.");
                }

                if (string.IsNullOrWhiteSpace(command.Name)
                        || command.Name.Trim().Length > DomainConstants.MAX_TEMPLATE_NAME_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Template name must be non-empty and cannot exceed {DomainConstants.MAX_TEMPLATE_NAME_LENGTH} characters.");
                }

                if (string.IsNullOrWhiteSpace(command.ActorId)
                        || command.ActorId.Trim().Length > DomainConstants.MAX_ACTOR_ID_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Actor ID must be non-empty and cannot exceed {DomainConstants.MAX_ACTOR_ID_LENGTH} characters.");
                }

                ReferencedImageVerifier.ValidateJsonObject(
                        jsonPayload: command.SchemaJson,
                        maxLength: DomainConstants.MAX_SCHEMA_JSON_LENGTH,
                        fieldName: ApplicationConstants.SCHEMA_JSON_FIELD_NAME);
        }
}

