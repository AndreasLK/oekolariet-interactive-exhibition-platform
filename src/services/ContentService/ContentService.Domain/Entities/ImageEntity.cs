using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents the metadata, content verification digest, and soft-delete lifecycle of a stored exhibition image.
/// </summary>
// Metadata lives here while raw image bytes live in ImageBinaryEntity so listing or searching images never loads megabytes of pixel data from the database.
public sealed class ImageEntity : BaseEntity
{
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageEntity"/> class for EF Core.
        /// </summary>
        private ImageEntity()
        {
        }

        /// <summary>
        /// Gets the storage node (shard) identifier where this image is persisted.
        /// </summary>
        public string StorageNodeId { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the sanitized original file name of the uploaded image.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the normalized lowercase MIME content type of the image.
        /// </summary>
        public string ContentType { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the verified byte length of the image payload.
        /// </summary>
        // Stored on metadata so quota checks and API headers do not have to load ImageBinaryEntity.
        public long FileSizeBytes { get; private set; }

        /// <summary>
        /// Gets the lowercase 64-character hexadecimal SHA-256 hash of the image payload.
        /// </summary>
        // Used to detect duplicate uploads and serve HTTP ETag headers so browsers/kiosks can skip re-downloading unchanged images.
        public string Sha256Hash { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the optional accessibility alternative text / description for the image, or <see langword="null"/> if not provided.
        /// </summary>
        public string? AltText { get; private set; }

        /// <summary>
        /// Gets the optional normalized recursive album path (e.g., <c>Madaffald/Frugt</c>), or <see langword="null"/> for root-level images.
        /// </summary>
        public string? AlbumPath { get; private set; }

        /// <summary>
        /// Gets the optional normalized comma-separated search tags, or <see langword="null"/> when untagged.
        /// </summary>
        public string? Tags { get; private set; }

        /// <summary>
        /// Gets the parsed read-only list of search tags associated with this image.
        /// </summary>
        public IReadOnlyList<string> TagList => ImageTags.Parse(normalizedTags: this.Tags);

        /// <summary>
        /// Gets a value indicating whether this image has been soft-deleted.
        /// </summary>
        // Soft-delete keeps metadata intact so drafts stored in EditorService's separate database do not break when an image is retired.
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// Gets the external route identifier (<c>{node}_{guid}</c>) for this image.
        /// </summary>
        public string ExternalId => this.Reference.ToExternalImageId();

        /// <summary>
        /// Gets the structured <see cref="PictureReference"/> handle (<c>picture.{node}.{guid}</c>) for this image.
        /// </summary>
        // Two formats exist because ExternalId ('_') fits clean HTTP URLs whereas PictureReference ('picture.{node}.{guid}') stands out inside template JSON.
        public PictureReference Reference => new(this.StorageNodeId, this.Id);

        /// <summary>
        /// Creates a validated <see cref="ImageEntity"/> with initialized audit metadata.
        /// </summary>
        /// <param name="storageNodeId">The storage node identifier owning the image.</param>
        /// <param name="fileName">The uploaded image file name.</param>
        /// <param name="contentType">The MIME content type of the image.</param>
        /// <param name="fileSizeBytes">The byte size of the image binary payload.</param>
        /// <param name="sha256Hash">The 64-character hexadecimal SHA-256 digest of the image payload.</param>
        /// <param name="actorId">The identifier of the actor creating the image.</param>
        /// <param name="altText">Optional accessibility alternative text / description.</param>
        /// <param name="albumPath">Optional normalized recursive album path.</param>
        /// <param name="tags">Optional comma-separated search tags.</param>
        /// <returns>A newly created and validated <see cref="ImageEntity"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when any metadata field or <paramref name="actorId"/> violates domain invariants.</exception>
        public static ImageEntity Create(
                string storageNodeId,
                string fileName,
                string contentType,
                long fileSizeBytes,
                string sha256Hash,
                string actorId,
                string? altText = null,
                string? albumPath = null,
                string? tags = null)
        {
                // Validate every field before creating the entity so an invalid ImageEntity can never exist in memory.
                string validatedStorageNodeId = PictureReference.ValidateStorageNodeId(storageNodeId);
                string validatedFileName = ImageMetadataValidator.ValidateFileName(fileName: fileName);
                string validatedContentType = ImageMetadataValidator.ValidateContentType(contentType: contentType);
                ImageMetadataValidator.ValidateFileSize(fileSizeBytes: fileSizeBytes);
                string validatedHash = ImageMetadataValidator.ValidateSha256Hash(sha256Hash: sha256Hash);
                string? validatedAltText = ImageMetadataValidator.ValidateAltText(altText: altText);
                string? validatedAlbumPath = ValueObjects.AlbumPath.NormalizeOptional(rawAlbumPath: albumPath);
                string? validatedTags = ImageTags.NormalizeOptional(rawTags: tags);

                ImageEntity entity = new()
                {
                        StorageNodeId = validatedStorageNodeId,
                        FileName = validatedFileName,
                        ContentType = validatedContentType,
                        FileSizeBytes = fileSizeBytes,
                        Sha256Hash = validatedHash,
                        AltText = validatedAltText,
                        AlbumPath = validatedAlbumPath,
                        Tags = validatedTags,
                        IsDeleted = false
                };

                entity.InitializeAudit(actorId);
                return entity;
        }

        /// <summary>
        /// Updates the image's description (<see cref="AltText"/>), search tags, and album location atomically with modification audit metadata.
        /// </summary>
        /// <param name="altText">The new alternative text / description, or blank/<see langword="null"/> to clear it.</param>
        /// <param name="tags">The new comma-separated tags, or blank/<see langword="null"/> to clear them.</param>
        /// <param name="albumPath">The new recursive album path, or blank/<see langword="null"/> for root.</param>
        /// <param name="actorId">The identifier of the actor performing the update.</param>
        /// <exception cref="DomainValidationException">Thrown when the image is deleted or any field violates domain invariants.</exception>
        public void UpdateMetadata(
                string? altText,
                string? tags,
                string? albumPath,
                string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException("Cannot update metadata of a deleted image.");
                }

                // Validate all inputs and actorId before mutating state so failed updates leave the entity unchanged.
                string? validatedAltText = ImageMetadataValidator.ValidateAltText(altText: altText);
                string? validatedTags = ImageTags.NormalizeOptional(rawTags: tags);
                string? validatedAlbumPath = ValueObjects.AlbumPath.NormalizeOptional(rawAlbumPath: albumPath);

                this.MarkModified(actorId: actorId);
                this.AltText = validatedAltText;
                this.Tags = validatedTags;
                this.AlbumPath = validatedAlbumPath;
        }

        /// <summary>
        /// Updates the image's optional accessibility alternative text and records modification audit metadata.
        /// </summary>
        /// <param name="altText">The new alternative text, or blank/<see langword="null"/> to clear it.</param>
        /// <param name="actorId">The identifier of the actor performing the update.</param>
        /// <exception cref="DomainValidationException">Thrown when the image is already deleted, <paramref name="altText"/> exceeds the length limit, or <paramref name="actorId"/> is invalid.</exception>
        public void UpdateAltText(string? altText, string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException("Cannot update alternative text of a deleted image.");
                }

                // Validate both altText and actorId before mutating AltText so failed calls leave the entity unchanged.
                string? validatedAltText = ImageMetadataValidator.ValidateAltText(altText: altText);
                this.MarkModified(actorId);
                this.AltText = validatedAltText;
        }

        /// <summary>
        /// Marks the image as soft-deleted and records modification audit metadata.
        /// </summary>
        /// <param name="actorId">The identifier of the actor deleting the image.</param>
        /// <exception cref="DomainValidationException">Thrown when the image is already deleted or <paramref name="actorId"/> is invalid.</exception>
        public void MarkDeleted(string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException("Image is already deleted.");
                }

                // Validate actorId via MarkModified before flipping IsDeleted so invalid calls do not partially mutate state.
                this.MarkModified(actorId);
                this.IsDeleted = true;
        }
}
