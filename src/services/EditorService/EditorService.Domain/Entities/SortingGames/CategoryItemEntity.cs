using EditorService.Domain.Common;
using EditorService.Domain.Exceptions;


namespace EditorService.Domain.Entities.SortingGames
{
        /// <summary>
        /// Represents an item in a sorting game, e.g. a piece of waste, that the player must drag into the correct category receiver.
        /// Part of the <see cref="SortingGameEntity"/> aggregate and only changed through it.
        /// </summary>
        public sealed class CategoryItemEntity
        {
                /// <summary>Gets the unique identifier of the category item.</summary>
                public Guid Id { get; private set; }

                /// <summary>Gets the name of the category item, e.g. "Milk carton".</summary>
                public string Name { get; private set; } = string.Empty;

                /// <summary>Gets the explanation shown to the player after answering (F17).</summary>
                public string Explanation { get; private set; } = string.Empty;

                /// <summary>Gets the identifier of the image in Content Service.</summary>
                public Guid ImageId { get; private set; }

                /// <summary>Gets the identifier of the category receiver this item belongs in.</summary>
                public Guid CategoryReceiverId { get; private set; }

                /// <summary>
                /// Initializes a new instance of the <see cref="CategoryItemEntity"/> class for EF Core.
                /// Use <see cref="Create"/> to make a new category item.
                /// </summary>
                private CategoryItemEntity()
                {
                }

                /// <summary>
                /// Creates a new category item after checking all values.
                /// </summary>
                /// <param name="name">The name of the category item.</param>
                /// <param name="explanation">The explanation shown to the player.</param>
                /// <param name="imageId">The identifier of the image in Content Service.</param>
                /// <param name="categoryReceiverId">The identifier of the correct category receiver.</param>
                /// <returns>A new <see cref="CategoryItemEntity"/>.</returns>
                internal static CategoryItemEntity Create(string name, string explanation, Guid imageId, Guid categoryReceiverId)
                {
                        CategoryItemEntity categoryItem = new()
                        {
                                Id = Guid.NewGuid(),
                                Name = ValidateName(value: name, maxLength: DomainConstants.MAX_CATEGORY_ITEM_NAME_LENGTH, fieldName: "Category item name"),
                                Explanation = ValidateName(value: explanation, maxLength: DomainConstants.MAX_EXPLANATION_LENGTH, fieldName: "Explanation"),
                                ImageId = ValidateReference(id: imageId, fieldName: "Image ID"),
                                CategoryReceiverId = ValidateReference(id: categoryReceiverId, fieldName: "Category receiver ID")
                        };
                        return categoryItem;
                }

                /// <summary>
                /// Changes the image of the category item.
                /// </summary>
                /// <param name="imageId">The identifier of the new image in Content Service.</param>
                internal void UpdateImage(Guid imageId)
                {
                        this.ImageId = ValidateReference(id: imageId, fieldName: "Image ID");
                }

                /// <summary>
                /// Changes the name of the category item.
                /// </summary>
                /// <param name="name">The new name.</param>
                internal void UpdateName(string name)
                {
                        this.Name = ValidateName(value: name, maxLength: DomainConstants.MAX_CATEGORY_ITEM_NAME_LENGTH, fieldName: "Category item name");
                }

                /// <summary>
                /// Changes the explanation shown to the player.
                /// </summary>
                /// <param name="explanation">The new explanation.</param>
                internal void UpdateExplanation(string explanation)
                {
                        this.Explanation = ValidateName(value: explanation, maxLength: DomainConstants.MAX_EXPLANATION_LENGTH, fieldName: "Explanation");
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

                /// <summary>
                /// Checks that an identifier is not empty.
                /// </summary>
                /// <param name="id">The identifier to check.</param>
                /// <param name="fieldName">The name of the field, used in the error message.</param>
                /// <returns>The identifier, if it is valid.</returns>
                private static Guid ValidateReference(Guid id, string fieldName)
                {
                        if (id == Guid.Empty)
                        {
                                throw new DomainValidationException(message: $"{fieldName} cannot be an empty GUID.");
                        }
                        return id;
                }
        }
}
