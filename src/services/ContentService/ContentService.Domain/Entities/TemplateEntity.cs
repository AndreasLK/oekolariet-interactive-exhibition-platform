using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents a reusable exhibition content template with a versioned JSON schema.
/// </summary>
public sealed class TemplateEntity : BaseEntity
{
        private TemplateEntity()
        {
        }

        /// <summary>
        /// Gets the display name of the template.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the UI component type rendered for this template.
        /// </summary>
        public TemplateComponentType ComponentType { get; private set; }

        /// <summary>
        /// Gets the JSON schema defining the template configuration structure.
        /// </summary>
        public string SchemaJson { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the monotonically increasing version number of the template schema.
        /// </summary>
        public int SchemaVersion { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the template has been archived.
        /// </summary>
        public bool IsArchived { get; private set; }

        /// <summary>
        /// Creates a new <see cref="TemplateEntity"/> with an initial schema version.
        /// </summary>
        /// <param name="name">The display name of the template.</param>
        /// <param name="componentType">The UI component type of the template.</param>
        /// <param name="schemaJson">The JSON schema definition.</param>
        /// <param name="actorId">The identifier of the actor creating the template.</param>
        /// <returns>A newly initialized <see cref="TemplateEntity"/>.</returns>
        public static TemplateEntity Create(
                string name,
                TemplateComponentType componentType,
                string schemaJson,
                string actorId)
        {
                string validatedName = ValidateName(name: name);
                string validatedSchemaJson = ValidateSchemaJson(schemaJson: schemaJson);

                TemplateEntity entity = new()
                {
                        Name = validatedName,
                        ComponentType = componentType,
                        SchemaJson = validatedSchemaJson,
                        SchemaVersion = DomainConstants.INITIAL_VERSION,
                        IsArchived = false
                };

                entity.InitializeAudit(actorId: actorId);
                return entity;
        }

        /// <summary>
        /// Updates the template name and schema JSON, incrementing <see cref="SchemaVersion"/>.
        /// </summary>
        /// <param name="name">The updated display name.</param>
        /// <param name="schemaJson">The updated JSON schema definition.</param>
        /// <param name="actorId">The identifier of the actor updating the template.</param>
        public void UpdateSchema(string name, string schemaJson, string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException(message: "Cannot update an archived template.");
                }

                this.Name = ValidateName(name: name);
                this.SchemaJson = ValidateSchemaJson(schemaJson: schemaJson);
                this.SchemaVersion++;
                this.MarkModified(actorId: actorId);
        }

        /// <summary>
        /// Archives the template so it can no longer be modified or used for new drafts.
        /// </summary>
        /// <param name="actorId">The identifier of the actor archiving the template.</param>
        public void Archive(string actorId)
        {
                if (this.IsArchived)
                {
                        throw new DomainValidationException(message: "Template is already archived.");
                }

                this.IsArchived = true;
                this.MarkModified(actorId: actorId);
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
