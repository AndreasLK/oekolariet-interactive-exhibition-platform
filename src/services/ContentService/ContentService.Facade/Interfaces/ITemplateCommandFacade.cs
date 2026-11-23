using ContentService.Facade.Dtos;

namespace ContentService.Facade.Interfaces;

/// <summary>
/// CQS command facade contract for state-mutating exhibition template operations.
/// </summary>
public interface ITemplateCommandFacade
{
        /// <summary>
        /// Creates a new versioned exhibition template.
        /// </summary>
        /// <param name="command">The template creation command DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during persistence.</param>
        /// <returns>The created <see cref="TemplateMetadataDto"/>.</returns>
        public Task<TemplateMetadataDto> CreateTemplateAsync(
                CreateTemplateCommandDto command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing exhibition template with optimistic concurrency enforcement.
        /// </summary>
        /// <param name="command">The template update command DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during persistence.</param>
        /// <returns>The updated <see cref="TemplateMetadataDto"/>.</returns>
        public Task<TemplateMetadataDto> UpdateTemplateAsync(
                UpdateTemplateCommandDto command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives an active exhibition template.
        /// </summary>
        /// <param name="command">The template archival command DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during archival.</param>
        /// <returns>A task representing the asynchronous archival operation.</returns>
        public Task DeleteTemplateAsync(
                DeleteTemplateCommandDto command,
                CancellationToken cancellationToken = default);
}

