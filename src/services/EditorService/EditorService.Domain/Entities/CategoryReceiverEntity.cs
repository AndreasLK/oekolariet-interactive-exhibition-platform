using EditorService.Domain.Common;
using EditorService.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Entities
{
        /// <summary>
        /// Represents a target in a sorting game that items are sorted into, e.g. a bin.
        /// A category receiver is shown on the screen with a color, an image, or both.
        /// Part of the <see cref="SortingGameEntity"/> aggregate and only changed through it.
        /// </summary>
        public sealed class CategoryReceiverEntity
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="CategoryReceiverEntity"/> class for EF Core.
                /// Use <see cref="Create"/> to make a new category receiver.
                /// </summary>
                private CategoryReceiverEntity()
                {
                }

                /// <summary>Gets the unique identifier of the category receiver.</summary>
                public Guid Id { get; private set; }

                /// <summary>Gets the name of the category receiver, e.g. "Plastic".</summary>
                public string Name { get; private set; } = string.Empty;

                /// <summary>Gets the color of the category receiver, e.g. "#1D9E75", or <see langword="null"/> if it uses an image.</summary>
                public string? Color { get; private set; }

                /// <summary>Gets the identifier of the image in Content Service, or <see langword="null"/> if it uses a color.</summary>
                public Guid? ImageId { get; private set; }

                /// <summary>
                /// Creates a new category receiver after checking all values.
                /// </summary>
                /// <param name="name">The name of the category receiver.</param>
                /// <param name="color">The color of the category receiver, or <see langword="null"/>.</param>
                /// <param name="imageId">The identifier of the image in Content Service, or <see langword="null"/>.</param>
                /// <returns>A new <see cref="CategoryReceiverEntity"/>.</returns>
                internal static CategoryReceiverEntity Create(string name, string? color, Guid? imageId)
                {
                        CategoryReceiverEntity category = new()
                        {
                                Id = Guid.NewGuid(),
                                Name = ValidateName(value: name, maxLength: DomainConstants.MAX_CATEGORY_RECEIVER_NAME_LENGTH, fieldName: "Category receiver name")
                        };

                        category.UpdateAppearance(color: color, imageId: imageId);
                        return category;
                }

                /// <summary>
                /// Changes the name of the category receiver.
                /// </summary>
                /// <param name="name">The new name.</param>
                internal void UpdateName(string name)
                {
                        this.Name = ValidateName(value: name, maxLength: DomainConstants.MAX_CATEGORY_RECEIVER_NAME_LENGTH, fieldName: "Category receiver name");
                }

                /// <summary>
                /// Changes how the category receiver looks on the screen: a color, an image, or both.
                /// </summary>
                /// <param name="color">The new color, or <see langword="null"/>.</param>
                /// <param name="imageId">The new image identifier, or <see langword="null"/>.</param>
                internal void UpdateAppearance(string? color, Guid? imageId)
                {
                        // Treat an empty color the same as no color.
                        bool hasColor = !string.IsNullOrWhiteSpace(value: color);
                        bool hasImage = imageId is not null && imageId != Guid.Empty;

                        // The player must be able to see the category receiver, so it needs a color or an image.
                        if (!hasColor && !hasImage)
                        {
                                throw new DomainValidationException(message: "A category receiver must have a color, an image, or both.");
                        }

                        // Validate first, then set, so the category receiver is never left half-changed.
                        string? validatedColor = hasColor
                                ? ValidateName(value: color!, maxLength: DomainConstants.MAX_COLOR_LENGTH, fieldName: "Category receiver color")
                                : null;

                        this.Color = validatedColor;
                        this.ImageId = hasImage ? imageId : null;
                }

                /// <summary>
                /// Checks that a text is not empty and not too long, and returns it without spaces at the ends.
                /// </summary>
                /// <param name="value">The text to check.</param>
                /// <param name="maxLength">The maximum number of characters allowed.</param>
                /// <param name="fieldName">The name of the field, used in the error message.</param>
                /// <returns>The trimmed text.</returns>
                private static string ValidateName(string value, int maxLength, string fieldName)
                {
                        if (string.IsNullOrWhiteSpace(value: value))
                        {
                                throw new DomainValidationException(message: $"{fieldName} cannot be null or whitespace.");
                        }

                        string trimmed = value.Trim();
                        if (trimmed.Length > maxLength)
                        {
                                throw new DomainValidationException(message: $"{fieldName} cannot exceed {maxLength} characters.");
                        }

                        return trimmed;
                }
        }
}

