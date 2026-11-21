using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents a versioned exhibition template aggregate root with indexed image dependencies.
/// </summary>
// Stores raw SchemaJson instead of rigid C# schema classes so ContentService stays decoupled from UI layout changes while still indexing embedded image handles.
public sealed class TemplateEntity : BaseEntity
{
        /// <summary>
        /// Backing list of <see cref="TemplateImageReference"/> join entries owned by this template.
        /// </summary>
        // Exposed via AsReadOnly() so EF Core can populate the field while external callers cannot bypass template validation.
        private readonly List<TemplateImageReference> _referencedImages = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateEntity"/> class for EF Core.
        /// </summary>
        private TemplateEntity()
        {
        }

        /// <summary>
        /// Gets the storage node identifier where this template is stored.
        /// </summary>
        public string StorageNodeId { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the display name of the template.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Blazor UI component category associated with this template.
        /// </summary>
        // Immutable after creation because changing component type would break existing exhibition drafts in EditorService; create a new template instead.
        public TemplateComponentType ComponentType { get; private set; }

        /// <summary>
        /// Gets the raw JSON schema payload defining the template structure and picture handles.
        /// </summary>
        public string SchemaJson { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the monotonically increasing schema version incremented on every schema update.
        /// </summary>
        // Lets the UI Editor detect when a template has changed relative to an authored draft.
        public int SchemaVersion { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this template has been archived.
        /// </summary>
        // Soft-archiving preserves template history so active exhibition drafts referencing older templates do not break.
        public bool IsArchived { get; private set; }

        /// <summary>
        /// Gets the external route identifier (<c>{node}_{guid}</c>) for this template.
        /// </summary>
        public string ExternalId => $"{this.StorageNodeId}{DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR}{this.Id.ToString(DomainConstants.GUID_FORMAT_HYPHENATED)}";

        /// <summary>
        /// Gets the read-only collection of image references extracted from <see cref="SchemaJson"/>.
        /// </summary>
        public IReadOnlyCollection<TemplateImageReference> ReferencedImages => this._referencedImages.AsReadOnly();

        /// <summary>
        /// Creates a validated <see cref="TemplateEntity"/>, initializes audit metadata, and indexes referenced images.
        /// </summary>
        /// <param name="storageNodeId">The storage node identifier owning the template.</param>
        /// <param name="name">The template display name.</param>
        /// <param name="componentType">The target Blazor UI component category.</param>
        /// <param name="schemaJson">The raw JSON schema payload.</param>
        /// <param name="actorId">The identifier of the actor creating the template.</param>
        /// <returns>A newly created and validated <see cref="TemplateEntity"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when any argument violates template invariants or <paramref name="schemaJson"/> contains embedded base64 images.</exception>
        public static TemplateEntity Create(
                string storageNodeId,
                string name,
                TemplateComponentType componentType,
                string schemaJson,
                string actorId)
        {
                string validatedStorageNodeId = PictureReference.ValidateStorageNodeId(storageNodeId);
                string validatedName = ValidateName(name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson);
                List<PictureReference> referencedPictures = PictureReference.ExtractReferences(validatedSchemaJson);

                TemplateEntity entity = new()
                {
                        StorageNodeId = validatedStorageNodeId,
                        Name = validatedName,
                        ComponentType = componentType,
                        SchemaJson = validatedSchemaJson,
                        SchemaVersion = DomainConstants.INITIAL_VERSION,
                        IsArchived = false
                };

                // InitializeAudit must run first so entity.Id is generated before building TemplateImageReference join entries.
                entity.InitializeAudit(actorId);
                entity.ReplaceReferencedImages(entity.BuildImageReferences(referencedPictures));
                return entity;
        }

        /// <summary>
        /// Updates the template name and schema JSON, increments <see cref="SchemaVersion"/>, and re-synchronizes indexed image references.
        /// </summary>
        /// <param name="name">The updated template display name.</param>
        /// <param name="schemaJson">The updated raw JSON schema payload.</param>
        /// <param name="actorId">The identifier of the actor performing the update.</param>
        /// <exception cref="DomainValidationException">Thrown when the template is archived or any input violates domain invariants.</exception>
        public void UpdateSchema(string name, string schemaJson, string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException("Cannot update an archived template.");
                }

                // Validate all inputs and build the new reference list before mutating any state so a failed update leaves the entity untouched.
                string validatedName = ValidateName(name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson);
                List<PictureReference> referencedPictures = PictureReference.ExtractReferences(validatedSchemaJson);
                List<TemplateImageReference> updatedReferences = this.BuildImageReferences(referencedPictures);

                this.MarkModified(actorId);
                this.Name = validatedName;
                this.SchemaJson = validatedSchemaJson;
                this.SchemaVersion++;
                this.ReplaceReferencedImages(updatedReferences);
        }

        /// <summary>
        /// Marks the template as archived and records modification audit metadata.
        /// </summary>
        /// <param name="actorId">The identifier of the actor archiving the template.</param>
        /// <exception cref="DomainValidationException">Thrown when the template is already archived or <paramref name="actorId"/> is invalid.</exception>
        public void Archive(string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException("Template is already archived.");
                }

                this.MarkModified(actorId);
                this.IsArchived = true;
        }

        /// <summary>
        /// Builds <see cref="TemplateImageReference"/> join entries for the extracted picture handles.
        /// </summary>
        /// <param name="references">The deduplicated list of picture references extracted from <see cref="SchemaJson"/>.</param>
        /// <returns>A list of validated <see cref="TemplateImageReference"/> entries.</returns>
        private List<TemplateImageReference> BuildImageReferences(List<PictureReference> references)
        {
                List<TemplateImageReference> joinEntries = new(references.Count);
                foreach (PictureReference reference in references)
                {
                        joinEntries.Add(TemplateImageReference.Create(this.Id, reference));
                }

                return joinEntries;
        }

        /// <summary>
        /// Replaces the tracked <see cref="TemplateImageReference"/> collection with the prepared entries.
        /// </summary>
        /// <param name="updatedReferences">The newly built join entries.</param>
        private void ReplaceReferencedImages(List<TemplateImageReference> updatedReferences)
        {
                // Clear-and-rebuild is simpler and safer than diffing because TemplateImageReference is a pure link row with no independent audit fields.
                this._referencedImages.Clear();
                this._referencedImages.AddRange(updatedReferences);
        }

        /// <summary>
        /// Validates and trims the template display name.
        /// </summary>
        /// <param name="name">The raw template name.</param>
        /// <returns>The trimmed template name within <see cref="DomainConstants.MAX_TEMPLATE_NAME_LENGTH"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="name"/> is blank or exceeds the maximum length.</exception>
        private static string ValidateName(string name)
        {
                if (string.IsNullOrWhiteSpace(name))
                {
                        throw new DomainValidationException("Template name cannot be null or whitespace.");
                }

                string trimmed = name.Trim();
                if (trimmed.Length > DomainConstants.MAX_TEMPLATE_NAME_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Template name cannot exceed {DomainConstants.MAX_TEMPLATE_NAME_LENGTH} characters.");
                }

                return trimmed;
        }

        /// <summary>
        /// Validates that template schema JSON is non-empty and within the maximum permitted character limit.
        /// </summary>
        /// <param name="schemaJson">The raw JSON schema payload.</param>
        /// <returns>The unmodified <paramref name="schemaJson"/> string.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="schemaJson"/> is blank or exceeds <see cref="DomainConstants.MAX_SCHEMA_JSON_LENGTH"/>.</exception>
        private static string ValidateSchemaJson(string schemaJson)
        {
                if (string.IsNullOrWhiteSpace(schemaJson))
                {
                        throw new DomainValidationException("Schema JSON cannot be null or whitespace.");
                }

                if (schemaJson.Length > DomainConstants.MAX_SCHEMA_JSON_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Schema JSON cannot exceed {DomainConstants.MAX_SCHEMA_JSON_LENGTH} characters.");
                }

                // Preserve schemaJson untrimmed so authored JSON formatting is never altered.
                return schemaJson;
        }
}
