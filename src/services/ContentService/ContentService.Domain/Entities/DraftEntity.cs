using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents an authored exhibition content draft bound to a <see cref="TemplateEntity"/>.
/// </summary>
public sealed class DraftEntity : BaseEntity
{
        private DraftEntity()
        {
        }

        /// <summary>
        /// Gets the identifier of the template this draft is based on.
        /// </summary>
        public Guid TemplateId { get; private set; }

        /// <summary>
        /// Gets the template schema version this draft targets.
        /// </summary>
        public int TargetTemplateSchemaVersion { get; private set; }

        /// <summary>
        /// Gets the display title of the draft.
        /// </summary>
        public string Title { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the JSON configuration payload for the draft.
        /// </summary>
        public string ConfigurationJson { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the current lifecycle status of the draft.
        /// </summary>
        public DraftStatus Status { get; private set; }

        /// <summary>
        /// Gets the published version number, or <see langword="null"/> if never published.
        /// </summary>
        public int? PublishedVersion { get; private set; }

        /// <summary>
        /// Gets the UTC timestamp when the draft was last published, or <see langword="null"/> if never published.
        /// </summary>
        public DateTimeOffset? PublishedAt { get; private set; }

        /// <summary>
        /// Gets the list of distinct image identifiers referenced in <see cref="ConfigurationJson"/>.
        /// </summary>
        public List<Guid> ReferencedImageIds { get; private set; } = [];

        /// <summary>
        /// Creates a new <see cref="DraftEntity"/> in the <see cref="DraftStatus.Draft"/> state.
        /// </summary>
        /// <param name="templateId">The identifier of the template this draft targets.</param>
        /// <param name="targetTemplateSchemaVersion">The schema version of the target template.</param>
        /// <param name="title">The display title of the draft.</param>
        /// <param name="configurationJson">The JSON configuration payload.</param>
        /// <param name="actorId">The identifier of the actor creating the draft.</param>
        /// <returns>A newly initialized <see cref="DraftEntity"/>.</returns>
        public static DraftEntity Create(
                Guid templateId,
                int targetTemplateSchemaVersion,
                string title,
                string configurationJson,
                string actorId)
        {
                if (templateId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Template ID cannot be empty.");
                }

                if (targetTemplateSchemaVersion < DomainConstants.INITIAL_VERSION)
                {
                        throw new DomainValidationException(
                                message: $"Target template schema version must be at least {DomainConstants.INITIAL_VERSION}.");
                }

                string validatedTitle = ValidateTitle(title: title);
                string validatedJson = ValidateConfigurationJson(configurationJson: configurationJson);
                List<Guid> referencedImageIds = PictureHandle.ExtractImageIds(jsonPayload: validatedJson);

                DraftEntity entity = new()
                {
                        TemplateId = templateId,
                        TargetTemplateSchemaVersion = targetTemplateSchemaVersion,
                        Title = validatedTitle,
                        ConfigurationJson = validatedJson,
                        Status = DraftStatus.Draft,
                        ReferencedImageIds = referencedImageIds
                };

                entity.InitializeAudit(actorId: actorId);
                return entity;
        }

        /// <summary>
        /// Updates the draft title, configuration JSON, and extracted image references.
        /// </summary>
        /// <param name="title">The updated display title.</param>
        /// <param name="configurationJson">The updated JSON configuration payload.</param>
        /// <param name="actorId">The identifier of the actor updating the draft.</param>
        public void UpdateConfiguration(string title, string configurationJson, string actorId)
        {
                if (this.Status != DraftStatus.Draft)
                {
                        throw new DomainValidationException(message: "Only drafts in Draft status can be edited.");
                }

                string validatedTitle = ValidateTitle(title: title);
                string validatedJson = ValidateConfigurationJson(configurationJson: configurationJson);
                List<Guid> referencedImageIds = PictureHandle.ExtractImageIds(jsonPayload: validatedJson);

                this.Title = validatedTitle;
                this.ConfigurationJson = validatedJson;
                this.ReferencedImageIds = referencedImageIds;
                this.MarkModified(actorId: actorId);
        }

        /// <summary>
        /// Publishes the draft and increments its published version.
        /// </summary>
        /// <param name="actorId">The identifier of the actor publishing the draft.</param>
        public void Publish(string actorId)
        {
                if (this.Status != DraftStatus.Draft)
                {
                        throw new DomainValidationException(message: "Only drafts in Draft status can be published.");
                }

                this.Status = DraftStatus.Published;
                this.PublishedVersion = (this.PublishedVersion ?? 0) + 1;
                this.PublishedAt = DateTimeOffset.UtcNow;
                this.MarkModified(actorId: actorId);
        }

        /// <summary>
        /// Reverts a published draft back to <see cref="DraftStatus.Draft"/> for further editing.
        /// </summary>
        /// <param name="actorId">The identifier of the actor reverting the draft.</param>
        public void RevertToDraft(string actorId)
        {
                if (this.Status != DraftStatus.Published)
                {
                        throw new DomainValidationException(message: "Only published drafts can be reverted to Draft status.");
                }

                this.Status = DraftStatus.Draft;
                this.MarkModified(actorId: actorId);
        }

        /// <summary>
        /// Archives the draft so it can no longer be edited or published.
        /// </summary>
        /// <param name="actorId">The identifier of the actor archiving the draft.</param>
        public void Archive(string actorId)
        {
                if (this.Status == DraftStatus.Archived)
                {
                        throw new DomainValidationException(message: "Draft is already archived.");
                }

                this.Status = DraftStatus.Archived;
                this.MarkModified(actorId: actorId);
        }

        private static string ValidateTitle(string title)
        {
                if (string.IsNullOrWhiteSpace(value: title))
                {
                        throw new DomainValidationException(message: "Draft title cannot be null or whitespace.");
                }

                string trimmed = title.Trim();
                if (trimmed.Length > DomainConstants.MAX_DRAFT_TITLE_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"Draft title cannot exceed {DomainConstants.MAX_DRAFT_TITLE_LENGTH} characters.");
                }

                return trimmed;
        }

        private static string ValidateConfigurationJson(string configurationJson)
        {
                if (string.IsNullOrWhiteSpace(value: configurationJson))
                {
                        throw new DomainValidationException(message: "Configuration JSON cannot be null or whitespace.");
                }

                if (configurationJson.Length > DomainConstants.MAX_CONFIGURATION_JSON_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"Configuration JSON cannot exceed {DomainConstants.MAX_CONFIGURATION_JSON_LENGTH} characters.");
                }

                return configurationJson;
        }
}
