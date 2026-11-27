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
/// Updates an existing exhibition template's name and schema JSON, reconciling indexed image dependencies and optimistic concurrency.
/// </summary>
public sealed class UpdateTemplateUseCase
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
        /// Optional peer storage node client used when updated templates reference images on other ContentService nodes.
        /// </summary>
        private readonly IStorageNodeClient? _storageNodeClient;

        /// <summary>
        /// Optional local storage node identifier used to distinguish local vs. peer node references.
        /// </summary>
        private readonly string? _localStorageNodeId;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateTemplateUseCase"/> class.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="storageNodeClient">Optional peer storage node client for cross-node image verification.</param>
        /// <param name="options">Optional content storage options supplying the local storage node identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public UpdateTemplateUseCase(
                ITemplateRepository templateRepository,
                IImageRepository imageRepository,
                IStorageNodeClient? storageNodeClient = null,
                IOptions<ContentStorageOptions>? options = null)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                ArgumentNullException.ThrowIfNull(imageRepository);

                this._templateRepository = templateRepository;
                this._imageRepository = imageRepository;
                this._storageNodeClient = storageNodeClient;
                this._localStorageNodeId = options?.Value.StorageNodeId;
        }

        /// <summary>
        /// Validates updated template state, verifies referenced images, increments schema version, and persists changes atomically.
        /// </summary>
        /// <param name="command">The update template command carrying expected concurrency stamp and schema updates.</param>
        /// <param name="cancellationToken">A token to observe during repository queries and persistence.</param>
        /// <returns>The updated <see cref="TemplateResult"/> reflecting the new schema version and concurrency stamp.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when the template is archived or inputs/JSON/uniqueness rules fail.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the target template or any referenced image does not exist.</exception>
        /// <exception cref="ConcurrencyConflictException">Thrown when <see cref="UpdateTemplateCommand.ExpectedConcurrencyStamp"/> is stale.</exception>
        public async Task<TemplateResult> ExecuteAsync(
                UpdateTemplateCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);
                ValidateCommandInputs(command: command);

                TemplateEntity? template = await this._templateRepository.GetByIdAsync(
                        id: command.TemplateId,
                        cancellationToken: cancellationToken);
                if (template is null)
                {
                        throw new KeyNotFoundException($"Template '{command.TemplateId}' was not found.");
                }

                // Fail fast on archived or stale templates before querying name uniqueness or resolving image dependencies.
                if (template.IsArchived)
                {
                        throw new DomainValidationException("Cannot update an archived template.");
                }

                if (template.ConcurrencyStamp != command.ExpectedConcurrencyStamp)
                {
                        throw new ConcurrencyConflictException(
                                $"Template '{command.TemplateId}' has been modified since it was last read.");
                }

                string trimmedName = command.Name.Trim();
                bool duplicateExists = await this._templateRepository.ExistsByNameAsync(
                        name: trimmedName,
                        excludeTemplateId: command.TemplateId,
                        cancellationToken: cancellationToken);
                if (duplicateExists)
                {
                        throw new DomainValidationException($"Another template named '{trimmedName}' already exists.");
                }

                List<PictureReference> referencedPictures = PictureReference.ExtractReferences(jsonPayload: command.SchemaJson);
                await ReferencedImageVerifier.VerifyAndResolveAsync(
                        imageRepository: this._imageRepository,
                        references: referencedPictures,
                        cancellationToken: cancellationToken,
                        storageNodeClient: this._storageNodeClient,
                        localStorageNodeId: this._localStorageNodeId);

                // UpdateSchema rotates template.ConcurrencyStamp in memory and trims Name; pass the original ExpectedConcurrencyStamp so the repository enforces optimistic concurrency.
                template.UpdateSchema(
                        name: command.Name,
                        schemaJson: command.SchemaJson,
                        actorId: command.ActorId);

                await this._templateRepository.UpdateAsync(
                        template: template,
                        expectedConcurrencyStamp: command.ExpectedConcurrencyStamp,
                        cancellationToken: cancellationToken);

                return ContentResultMapper.ToTemplateResult(template: template);
        }

        /// <summary>
        /// Validates command identifiers, strings, and JSON object syntax prior to repository calls.
        /// </summary>
        /// <param name="command">The command payload to validate.</param>
        /// <exception cref="DomainValidationException">Thrown when any input fails domain validation rules.</exception>
        private static void ValidateCommandInputs(UpdateTemplateCommand command)
        {
                if (command.TemplateId == Guid.Empty)
                {
                        throw new DomainValidationException("Template ID cannot be empty.");
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

