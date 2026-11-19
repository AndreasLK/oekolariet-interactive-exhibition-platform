using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

public sealed class TemplateEntity : BaseEntity
{
        private readonly List<TemplateImageReference> _referencedImages = [];

        private TemplateEntity()
        {
        }

        public string NodeId { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;

        public TemplateComponentType ComponentType { get; private set; }

        public string SchemaJson { get; private set; } = string.Empty;

        // Monotonic schema versioning lets UI Editor detect when a template's structure changed relative to an authored draft without redeploying UI code.
        public int SchemaVersion { get; private set; }

        public bool IsArchived { get; private set; }

        public string ExternalId => $"{this.NodeId}{DomainConstants.ASSET_ID_SEPARATOR}{this.Id.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";

        public IReadOnlyCollection<TemplateImageReference> ReferencedImages => this._referencedImages.AsReadOnly();

        public static TemplateEntity Create(
                string nodeId,
                string name,
                TemplateComponentType componentType,
                string schemaJson,
                string actorId)
        {
                string validatedNodeId = PictureHandle.ValidateNodeId(nodeId: nodeId);
                string validatedName = ValidateName(name: name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson: schemaJson);
                List<PictureHandle> referencedHandles = PictureHandle.ExtractHandles(jsonPayload: validatedSchemaJson);

                TemplateEntity entity = new()
                {
                        NodeId = validatedNodeId,
                        Name = validatedName,
                        ComponentType = componentType,
                        SchemaJson = validatedSchemaJson,
                        SchemaVersion = DomainConstants.INITIAL_VERSION,
                        IsArchived = false
                };

                entity.InitializeAudit(actorId: actorId);
                entity.SyncReferencedImages(handles: referencedHandles);
                return entity;
        }

        public void UpdateSchema(string name, string schemaJson, string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException(message: "Cannot update an archived template.");
                }

                string validatedName = ValidateName(name: name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson: schemaJson);
                List<PictureHandle> referencedHandles = PictureHandle.ExtractHandles(jsonPayload: validatedSchemaJson);

                this.Name = validatedName;
                this.SchemaJson = validatedSchemaJson;
                this.SchemaVersion++;
                this.SyncReferencedImages(handles: referencedHandles);
                this.MarkModified(actorId: actorId);
        }

        public void Archive(string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException(message: "Template is already archived.");
                }

                this.IsArchived = true;
                this.MarkModified(actorId: actorId);
        }

        private void SyncReferencedImages(List<PictureHandle> handles)
        {
                this._referencedImages.Clear();

                foreach (PictureHandle handle in handles)
                {
                        this._referencedImages.Add(
                                item: TemplateImageReference.Create(
                                        templateId: this.Id,
                                        pictureHandle: handle));
                }
        }

        private static string ValidateName(string name)
        {
                if (string.IsNullOrWhiteSpace(value: name))
                {
                        throw new DomainValidationException(message: "Template name cannot be null or whitespace.");
                }

                string trimmed = name.Trim();
                if (trimmed.Length > DomainConstants.MAX_TEMPLATE_NAME_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"Template name cannot exceed {DomainConstants.MAX_TEMPLATE_NAME_LENGTH} characters.");
                }

                return trimmed;
        }

        private static string ValidateSchemaJson(string schemaJson)
        {
                if (string.IsNullOrWhiteSpace(value: schemaJson))
                {
                        throw new DomainValidationException(message: "Schema JSON cannot be null or whitespace.");
                }

                if (schemaJson.Length > DomainConstants.MAX_SCHEMA_JSON_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"Schema JSON cannot exceed {DomainConstants.MAX_SCHEMA_JSON_LENGTH} characters.");
                }

                return schemaJson;
        }
}
