using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

// Explicit join entity backing the MSSQL TemplateImages many-to-many table so ContentService can index template dependencies and block deleting images still referenced by active templates.
public sealed class TemplateImageReference
{
        private TemplateImageReference()
        {
        }

        public Guid TemplateId { get; private set; }

        public Guid ImageId { get; private set; }

        public string ImageStorageNodeId { get; private set; } = string.Empty;

        public static TemplateImageReference Create(Guid templateId, PictureReference pictureReference)
        {
                if (templateId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Template ID cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(value: pictureReference.StorageNodeId) || pictureReference.ImageId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Picture reference must reference a valid node and image ID.");
                }

                return new TemplateImageReference
                {
                        TemplateId = templateId,
                        ImageId = pictureReference.ImageId,
                        ImageStorageNodeId = pictureReference.StorageNodeId
                };
        }
}

