using ContentService.Application.Models;
using ContentService.Application.UseCases.Templates;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Mapping;

namespace ContentService.Facade.Services;

/// <summary>
/// Default <see cref="ITemplateCommandFacade"/> orchestrating state-mutating template use cases across the facade boundary.
/// </summary>
internal sealed class TemplateCommandFacade : ITemplateCommandFacade
{
        /// <summary>
        /// Application use case creating new versioned templates.
        /// </summary>
        private readonly CreateTemplateUseCase _createTemplateUseCase;

        /// <summary>
        /// Application use case updating templates with optimistic concurrency enforcement.
        /// </summary>
        private readonly UpdateTemplateUseCase _updateTemplateUseCase;

        /// <summary>
        /// Application use case archiving templates.
        /// </summary>
        private readonly DeleteTemplateUseCase _deleteTemplateUseCase;

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateCommandFacade"/>.
        /// </summary>
        /// <param name="createTemplateUseCase">The create template use case.</param>
        /// <param name="updateTemplateUseCase">The update template use case.</param>
        /// <param name="deleteTemplateUseCase">The delete template use case.</param>
        public TemplateCommandFacade(
                CreateTemplateUseCase createTemplateUseCase,
                UpdateTemplateUseCase updateTemplateUseCase,
                DeleteTemplateUseCase deleteTemplateUseCase)
        {
                ArgumentNullException.ThrowIfNull(createTemplateUseCase);
                ArgumentNullException.ThrowIfNull(updateTemplateUseCase);
                ArgumentNullException.ThrowIfNull(deleteTemplateUseCase);

                this._createTemplateUseCase = createTemplateUseCase;
                this._updateTemplateUseCase = updateTemplateUseCase;
                this._deleteTemplateUseCase = deleteTemplateUseCase;
        }

        /// <inheritdoc />
        public async Task<TemplateMetadataDto> CreateTemplateAsync(
                CreateTemplateCommandDto command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                CreateTemplateCommand appCommand = new()
                {
                        Name = command.Name,
                        ComponentType = FacadeMapper.ToDomain(componentType: command.ComponentType),
                        SchemaJson = command.SchemaJson,
                        ActorId = command.ActorId
                };

                TemplateResult result = await this._createTemplateUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task<TemplateMetadataDto> UpdateTemplateAsync(
                UpdateTemplateCommandDto command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                UpdateTemplateCommand appCommand = new()
                {
                        TemplateId = command.TemplateId,
                        Name = command.Name,
                        SchemaJson = command.SchemaJson,
                        ExpectedConcurrencyStamp = command.ExpectedConcurrencyStamp,
                        ActorId = command.ActorId
                };

                TemplateResult result = await this._updateTemplateUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task DeleteTemplateAsync(
                DeleteTemplateCommandDto command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                DeleteTemplateCommand appCommand = new()
                {
                        TemplateId = command.TemplateId,
                        ActorId = command.ActorId
                };

                await this._deleteTemplateUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);
        }
}

