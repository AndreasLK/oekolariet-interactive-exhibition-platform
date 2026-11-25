using ContentService.Application.Interfaces;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Templates;

/// <summary>
/// Checks whether any local template (active or archived) references a given image ID so peer nodes can enforce cross-node delete protection.
/// </summary>
public sealed class CheckImageTemplateReferenceUseCase
{
        /// <summary>
        /// Local image/template-reference repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckImageTemplateReferenceUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public CheckImageTemplateReferenceUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Determines whether any template persisted on this node references <paramref name="imageId"/>.
        /// </summary>
        /// <param name="imageId">The image identifier to check.</param>
        /// <param name="cancellationToken">Propagates cancellation during repository lookup.</param>
        /// <returns><see langword="true"/> if referenced by at least one local template; otherwise <see langword="false"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="imageId"/> is <see cref="Guid.Empty"/>.</exception>
        public Task<bool> ExecuteAsync(
                Guid imageId,
                CancellationToken cancellationToken)
        {
                // Reject empty GUIDs up front before hitting the database.
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                // Query local template references (active and archived) for delete-protection checks.
                return this._imageRepository.IsReferencedByAnyTemplateAsync(
                        imageId: imageId,
                        cancellationToken: cancellationToken);
        }
}

