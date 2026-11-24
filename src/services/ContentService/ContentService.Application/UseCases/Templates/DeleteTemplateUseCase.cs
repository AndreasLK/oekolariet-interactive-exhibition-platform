using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Templates;

/// <summary>
/// Archives an active exhibition template while preserving historical rows for draft continuity.
/// </summary>
public sealed class DeleteTemplateUseCase
{
        /// <summary>
        /// Template aggregate repository.
        /// </summary>
        private readonly ITemplateRepository _templateRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteTemplateUseCase"/> class.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="templateRepository"/> is <see langword="null"/>.</exception>
        public DeleteTemplateUseCase(ITemplateRepository templateRepository)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                this._templateRepository = templateRepository;
        }

        /// <summary>
        /// Marks an active template as archived and persists the update with optimistic concurrency checking.
        /// </summary>
        /// <param name="command">The archive command identifying the template and acting user/service.</param>
        /// <param name="cancellationToken">A token to observe while loading and persisting the template.</param>
        /// <returns>A task representing the asynchronous archive operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when <see cref="DeleteTemplateCommand.TemplateId"/> is empty or <see cref="DeleteTemplateCommand.ActorId"/> is invalid.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the template does not exist or is already archived.</exception>
        /// <exception cref="ConcurrencyConflictException">Thrown when the template was concurrently modified before archiving completed.</exception>
        public async Task ExecuteAsync(DeleteTemplateCommand command, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                if (command.TemplateId == Guid.Empty)
                {
                        throw new DomainValidationException("Template ID cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(command.ActorId))
                {
                        throw new DomainValidationException("Actor ID cannot be null or whitespace.");
                }

                TemplateEntity? template = await this._templateRepository.GetByIdAsync(
                        id: command.TemplateId,
                        cancellationToken: cancellationToken);
                // Treat already-archived templates as missing so callers cannot distinguish archived IDs from nonexistent ones.
                if (template is null || template.IsArchived)
                {
                        throw new KeyNotFoundException($"Template '{command.TemplateId}' was not found.");
                }

                // Capture ConcurrencyStamp before Archive rotates it so the repository checks the pre-mutation token.
                Guid originalConcurrencyStamp = template.ConcurrencyStamp;
                template.Archive(actorId: command.ActorId);

                await this._templateRepository.DeleteAsync(
                        template: template,
                        expectedConcurrencyStamp: originalConcurrencyStamp,
                        cancellationToken: cancellationToken);
        }
}

