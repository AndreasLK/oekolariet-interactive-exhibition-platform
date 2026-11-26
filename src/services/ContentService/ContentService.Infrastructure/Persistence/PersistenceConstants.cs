namespace ContentService.Infrastructure.Persistence;

/// <summary>
/// Centralizes SQL Server table names, column bounds, backing-field identifiers, and error codes so EF configurations and repositories never drift or hardcode literals.
/// </summary>
internal static class PersistenceConstants
{
        /// <summary>
        /// Decouples <c>ImageEntity</c> schema mapping from literal table strings across configurations and migrations.
        /// </summary>
        public const string IMAGES_TABLE_NAME = "Images";

        /// <summary>
        /// Separates heavy <c>varbinary(max)</c> image payloads into their own table so metadata scans stay fast and lightweight.
        /// </summary>
        public const string IMAGE_BINARIES_TABLE_NAME = "ImageBinaries";

        /// <summary>
        /// Decouples <c>TemplateEntity</c> schema mapping from literal table strings across configurations and migrations.
        /// </summary>
        public const string TEMPLATES_TABLE_NAME = "Templates";

        /// <summary>
        /// Decouples the template-to-image link table name from raw strings across configurations and migrations.
        /// </summary>
        public const string TEMPLATE_IMAGE_REFERENCES_TABLE_NAME = "TemplateImageReferences";

        /// <summary>
        /// Bounds MIME type columns cleanly instead of allocating unbounded <c>nvarchar(max)</c> storage for short standardized strings.
        /// </summary>
        public const int MAX_CONTENT_TYPE_LENGTH = 64;

        /// <summary>
        /// Private list field name on <c>TemplateEntity</c> so EF Core can populate <c>ReferencedImages</c> without adding a public setter.
        /// </summary>
        public const string REFERENCED_IMAGES_FIELD_NAME = "_referencedImages";

        /// <summary>
        /// SQL Server error code for duplicate unique-index keys (mapped to HTTP 400 validation errors).
        /// </summary>
        public const int SQL_ERROR_DUPLICATE_INDEX_KEY = 2601;

        /// <summary>
        /// SQL Server error code for unique-constraint violations (mapped to HTTP 400 validation errors).
        /// </summary>
        public const int SQL_ERROR_UNIQUE_CONSTRAINT_VIOLATION = 2627;

        /// <summary>
        /// SQL Server error code for foreign-key constraint conflicts (e.g., referencing a deleted row).
        /// </summary>
        public const int SQL_ERROR_FOREIGN_KEY_CONFLICT = 547;

        /// <summary>
        /// Escape prefix used inside <c>EF.Functions.Like</c> patterns so user <c>%</c>, <c>_</c>, and <c>[</c> input stays literal.
        /// </summary>
        public const string LIKE_ESCAPE_CHAR = @"\";
}
