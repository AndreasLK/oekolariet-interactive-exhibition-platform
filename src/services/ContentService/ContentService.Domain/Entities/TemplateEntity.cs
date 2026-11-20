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

        public string StorageNodeId { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;

        public TemplateComponentType ComponentType { get; private set; }

        public string SchemaJson { get; private set; } = string.Empty;

        // Monotonic schema versioning lets UI Editor detect when a template's structure changed relative to an authored draft without redeploying UI code.
        public int SchemaVersion { get; private set; }

        public bool IsArchived { get; private set; }

        public string ExternalId => $"{this.StorageNodeId}{DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR}{this.Id.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";

        public IReadOnlyCollection<TemplateImageReference> ReferencedImages => this._referencedImages.AsReadOnly();

        // Factory method validates all invariants and extracts picture references upfront so a template can never be created in an invalid state.
        public static TemplateEntity Create(
                string storageNodeId,
                string name,
                TemplateComponentType componentType,
                string schemaJson,
                string actorId)
        {
                string validatedStorageNodeId = PictureReference.ValidateStorageNodeId(storageNodeId: storageNodeId);
                string validatedName = ValidateName(name: name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson: schemaJson);
                List<PictureReference> referencedPictures = PictureReference.ExtractReferences(jsonPayload: validatedSchemaJson);

                TemplateEntity entity = new()
                {
                        StorageNodeId = validatedStorageNodeId,
                        Name = validatedName,
                        ComponentType = componentType,
                        SchemaJson = validatedSchemaJson,
                        SchemaVersion = DomainConstants.INITIAL_VERSION,
                        IsArchived = false
                };

                entity.InitializeAudit(actorId: actorId);
                entity.SyncReferencedImages(references: referencedPictures);
                return entity;
        }

        // Increments SchemaVersion and re-syncs image references atomically whenever the template schema changes.
        public void UpdateSchema(string name, string schemaJson, string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException(message: "Cannot update an archived template.");
                }

                string validatedName = ValidateName(name: name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson: schemaJson);
                List<PictureReference> referencedPictures = PictureReference.ExtractReferences(jsonPayload: validatedSchemaJson);

                this.Name = validatedName;
                this.SchemaJson = validatedSchemaJson;
                this.SchemaVersion++;
                this.SyncReferencedImages(references: referencedPictures);
                this.MarkModified(actorId: actorId);
        }

        // Soft-archives instead of deleting so existing exhibition drafts referencing this template do not break.
        public void Archive(string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException(message: "Template is already archived.");
                }

                this.IsArchived = true;
                this.MarkModified(actorId: actorId);
        }

        private void SyncReferencedImages(List<PictureReference> references)
        {
                this._referencedImages.Clear();

                foreach (PictureReference reference in references)
                {
                        this._referencedImages.Add(
                                item: TemplateImageReference.Create(
                                        templateId: this.Id,
                                        pictureReference: reference));
                }
        }

        // Trims and bounds-checks the template name at the domain boundary so persistence and UI layers can rely on clean strings.
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

        // Guards against empty or oversized JSON payloads before regex scanning in PictureReference.ExtractReferences.
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
