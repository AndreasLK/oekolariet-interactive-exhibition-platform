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

        public string ImageNodeId { get; private set; } = string.Empty;

        public static TemplateImageReference Create(Guid templateId, PictureHandle pictureHandle)
        {
                if (templateId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Template ID cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(value: pictureHandle.NodeId) || pictureHandle.ImageId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Picture handle must reference a valid node and image ID.");
                }

                return new TemplateImageReference
                {
                        TemplateId = templateId,
                        ImageId = pictureHandle.ImageId,
                        ImageNodeId = pictureHandle.NodeId
                };
        }
}

