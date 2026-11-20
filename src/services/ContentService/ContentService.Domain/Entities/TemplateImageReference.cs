using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents an indexed join entry linking a <see cref="TemplateEntity"/> to a referenced image across storage nodes.
/// </summary>
// Stored as an indexed join table so deletion checks can query image usage in SQL without full-text scanning TemplateEntity.SchemaJson.
// Omits BaseEntity because it is a composite-key link owned exclusively by TemplateEntity with no separate Id or audit lifecycle.
public sealed class TemplateImageReference
{
        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateImageReference"/> class for EF Core.
        /// </summary>
        private TemplateImageReference()
        {
        }

        /// <summary>
        /// Gets the identifier of the referencing <see cref="TemplateEntity"/>.
        /// </summary>
        public Guid TemplateId { get; private set; }

        /// <summary>
        /// Gets the identifier of the referenced <see cref="ImageEntity"/>.
        /// </summary>
        public Guid ImageId { get; private set; }

        /// <summary>
        /// Gets the storage node identifier hosting the referenced image.
        /// </summary>
        // Stored alongside ImageId so templates on one shard can reference images stored on another shard.
        public string ImageStorageNodeId { get; private set; } = string.Empty;

        /// <summary>
        /// Creates a validated <see cref="TemplateImageReference"/> linking a template to a <see cref="PictureReference"/>.
        /// </summary>
        /// <param name="templateId">The non-empty identifier of the owning template.</param>
        /// <param name="pictureReference">The referenced image handle.</param>
        /// <returns>A newly constructed <see cref="TemplateImageReference"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="templateId"/> is empty or <paramref name="pictureReference"/> is null.</exception>
        public static TemplateImageReference Create(Guid templateId, PictureReference pictureReference)
        {
                if (templateId == Guid.Empty)
                {
                        throw new DomainValidationException("Template ID cannot be empty.");
                }

                if (pictureReference is null)
                {
                        throw new DomainValidationException("Picture reference cannot be null.");
                }

                return new TemplateImageReference
                {
                        TemplateId = templateId,
                        ImageId = pictureReference.ImageId,
                        ImageStorageNodeId = pictureReference.StorageNodeId
                };
        }
}
