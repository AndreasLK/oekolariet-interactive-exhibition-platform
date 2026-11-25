using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.Common;

/// <summary>
/// Maps domain entities to immutable application result records without duplicating projection logic across use cases.
/// </summary>
/// <remarks>
/// Kept internal rather than exposing an injectable mapping abstraction because projections are pure deterministic transformations.
/// </remarks>
internal static class ContentResultMapper
{
        /// <summary>
        /// Projects an <see cref="ImageEntity"/> to an <see cref="ImageMetadataResult"/>.
        /// </summary>
        /// <param name="image">The source image domain entity.</param>
        /// <returns>An immutable <see cref="ImageMetadataResult"/> representing the image metadata.</returns>
        public static ImageMetadataResult ToMetadataResult(ImageEntity image)
        {
                return new ImageMetadataResult
                {
                        Id = image.Id,
                        PictureReference = image.Reference.ToString(),
                        ExternalId = image.ExternalId,
                        FileName = image.FileName,
                        ContentType = image.ContentType,
                        SizeBytes = image.FileSizeBytes,
                        Sha256Hash = image.Sha256Hash,
                        AltText = image.AltText,
                        AlbumPath = image.AlbumPath,
                        Tags = image.TagList,
                        CreatedAt = image.CreatedAt,
                        ConcurrencyStamp = image.ConcurrencyStamp
                };
        }

        /// <summary>
        /// Projects an <see cref="ImageEntity"/> to a <see cref="BundleAssetResult"/> for offline publish packages.
        /// </summary>
        /// <param name="image">The resolved image domain entity.</param>
        /// <returns>An immutable <see cref="BundleAssetResult"/> describing the image asset.</returns>
        public static BundleAssetResult ToBundleAssetResult(ImageEntity image)
        {
                return new BundleAssetResult
                {
                        ImageId = image.Id,
                        PictureReference = image.Reference.ToString(),
                        ExternalId = image.ExternalId,
                        ContentType = image.ContentType,
                        SizeBytes = image.FileSizeBytes,
                        Sha256Hash = image.Sha256Hash
                };
        }

        /// <summary>
        /// Projects an <see cref="ImageMetadataResult"/> (from local or remote Dapr resolution) to a <see cref="BundleAssetResult"/>.
        /// </summary>
        /// <param name="image">The resolved image metadata projection.</param>
        /// <returns>An immutable <see cref="BundleAssetResult"/> describing the image asset.</returns>
        public static BundleAssetResult ToBundleAssetResult(ImageMetadataResult image)
        {
                return new BundleAssetResult
                {
                        ImageId = image.Id,
                        PictureReference = image.PictureReference,
                        ExternalId = image.ExternalId,
                        ContentType = image.ContentType,
                        SizeBytes = image.SizeBytes,
                        Sha256Hash = image.Sha256Hash
                };
        }

        /// <summary>
        /// Projects a <see cref="TemplateEntity"/> to a <see cref="TemplateResult"/> with sorted picture references.
        /// </summary>
        /// <param name="template">The source template domain entity.</param>
        /// <returns>An immutable <see cref="TemplateResult"/> representing the template state and referenced picture references.</returns>
        public static TemplateResult ToTemplateResult(TemplateEntity template)
        {
                // Format picture references via PictureReference so Application never re-implements the 'picture.{node}.{guid}' string convention.
                IReadOnlyList<string> pictureReferences = template.ReferencedImages
                        .Select(reference => new PictureReference(
                                storageNodeId: reference.ImageStorageNodeId,
                                imageId: reference.ImageId).ToString())
                        .OrderBy(keySelector: pictureReference => pictureReference, comparer: StringComparer.Ordinal)
                        .ToArray();

                return new TemplateResult
                {
                        Id = template.Id,
                        ExternalId = template.ExternalId,
                        Name = template.Name,
                        ComponentType = template.ComponentType,
                        SchemaJson = template.SchemaJson,
                        SchemaVersion = template.SchemaVersion,
                        IsArchived = template.IsArchived,
                        ReferencedPictureReferences = pictureReferences,
                        CreatedAt = template.CreatedAt,
                        UpdatedAt = template.UpdatedAt,
                        ConcurrencyStamp = template.ConcurrencyStamp
                };
        }
}

