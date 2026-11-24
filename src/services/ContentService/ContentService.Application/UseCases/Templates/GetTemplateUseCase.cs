using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Templates;

/// <summary>
/// Retrieves a stored exhibition template (active or archived) together with its indexed picture references.
/// </summary>
public sealed class GetTemplateUseCase
{
        /// <summary>
        /// Template aggregate repository.
        /// </summary>
        private readonly ITemplateRepository _templateRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetTemplateUseCase"/> class.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="templateRepository"/> is <see langword="null"/>.</exception>
        public GetTemplateUseCase(ITemplateRepository templateRepository)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                this._templateRepository = templateRepository;
        }

        /// <summary>
        /// Fetches the template identified by <paramref name="templateId"/>.
        /// </summary>
        /// <param name="templateId">The unique template identifier.</param>
        /// <param name="cancellationToken">A token to observe while querying the repository.</param>
        /// <returns>The projected <see cref="TemplateResult"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="templateId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no template with <paramref name="templateId"/> exists.</exception>
        public async Task<TemplateResult> ExecuteAsync(Guid templateId, CancellationToken cancellationToken)
        {
                if (templateId == Guid.Empty)
                {
                        throw new DomainValidationException("Template ID cannot be empty.");
                }

                // Archived templates remain readable because active drafts in EditorService may still reference older templates.
                TemplateEntity? template = await this._templateRepository.GetByIdAsync(
                        id: templateId,
                        cancellationToken: cancellationToken);
                if (template is null)
                {
                        throw new KeyNotFoundException($"Template '{templateId}' was not found.");
                }

                return ContentResultMapper.ToTemplateResult(template: template);
        }
}

