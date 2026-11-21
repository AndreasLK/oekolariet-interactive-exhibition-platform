using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Tests;

/// <summary>
/// Unit tests for <see cref="TemplateEntity"/> schema versioning, image reference sync, atomicity, and archiving.
/// </summary>
public sealed class TemplateEntityTests
{
        private const string VALID_NODE_ID = "node-1";
        private const string VALID_ACTOR_ID = "curator-1";
        private static readonly Guid FIRST_IMAGE_ID = Guid.Parse("11111111-1111-4111-8111-111111111111");
        private static readonly Guid SECOND_IMAGE_ID = Guid.Parse("22222222-2222-4222-8222-222222222222");

        /// <summary>
        /// Verifies that <see cref="TemplateEntity.Create"/> initializes version 1, preserves untrimmed JSON, and indexes image references.
        /// </summary>
        [Fact]
        public void Create_WhenValid_InitializesVersionAndIndexesReferencedImages()
        {
                // Arrange
                string handle = new PictureReference(VALID_NODE_ID, FIRST_IMAGE_ID).ToString();
                string rawSchema = $"  {{\"img\":\"{handle}\"}}  ";

                // Act
                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "  Quiz Intro  ",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: rawSchema,
                        actorId: VALID_ACTOR_ID);

                // Assert
                Assert.Equal("Quiz Intro", template.Name);
                Assert.Equal(rawSchema, template.SchemaJson);
                Assert.Equal(DomainConstants.INITIAL_VERSION, template.SchemaVersion);
                Assert.False(template.IsArchived);

                TemplateImageReference indexed = Assert.Single(template.ReferencedImages);
                Assert.Equal(template.Id, indexed.TemplateId);
                Assert.Equal(FIRST_IMAGE_ID, indexed.ImageId);
                Assert.Equal(VALID_NODE_ID, indexed.ImageStorageNodeId);
        }

        /// <summary>
        /// Verifies that <see cref="TemplateEntity.UpdateSchema"/> increments <see cref="TemplateEntity.SchemaVersion"/> and replaces indexed image references.
        /// </summary>
        [Fact]
        public void UpdateSchema_WhenValid_IncrementsVersionAndReplacesReferencedImages()
        {
                // Arrange
                TemplateEntity template = CreateValidTemplate();
                string nextHandle = new PictureReference("node-2", SECOND_IMAGE_ID).ToString();
                string nextSchema = $"{{\"banner\":\"{nextHandle}\"}}";

                // Act
                template.UpdateSchema("Updated Quiz", nextSchema, "editor-2");

                // Assert
                Assert.Equal("Updated Quiz", template.Name);
                Assert.Equal(nextSchema, template.SchemaJson);
                Assert.Equal(DomainConstants.INITIAL_VERSION + 1, template.SchemaVersion);

                TemplateImageReference indexed = Assert.Single(template.ReferencedImages);
                Assert.Equal(SECOND_IMAGE_ID, indexed.ImageId);
                Assert.Equal("node-2", indexed.ImageStorageNodeId);
        }

        /// <summary>
        /// Verifies that a failed <see cref="TemplateEntity.UpdateSchema"/> call leaves all properties and <see cref="TemplateEntity.ReferencedImages"/> untouched.
        /// </summary>
        [Fact]
        public void UpdateSchema_WhenSchemaContainsBase64Image_LeavesEntityUnchanged()
        {
                // Arrange
                TemplateEntity template = CreateValidTemplate();
                string originalName = template.Name;
                string originalSchema = template.SchemaJson;
                int originalVersion = template.SchemaVersion;
                Guid originalStamp = template.ConcurrencyStamp;
                string invalidSchema = $"{{\"src\":\"{DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER}png;base64,AAAA\"}}";

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => template.UpdateSchema("Broken", invalidSchema, "editor-2"));
                Assert.Equal(originalName, template.Name);
                Assert.Equal(originalSchema, template.SchemaJson);
                Assert.Equal(originalVersion, template.SchemaVersion);
                Assert.Equal(originalStamp, template.ConcurrencyStamp);
                Assert.Single(template.ReferencedImages);
        }

        /// <summary>
        /// Verifies that archiving a template blocks further schema updates and repeat archive calls.
        /// </summary>
        [Fact]
        public void Archive_WhenArchived_BlocksUpdateSchemaAndRepeatArchive()
        {
                // Arrange
                TemplateEntity template = CreateValidTemplate();

                // Act
                template.Archive(VALID_ACTOR_ID);

                // Assert
                Assert.True(template.IsArchived);
                Assert.Throws<DomainValidationException>(() => template.UpdateSchema("New", "{}", VALID_ACTOR_ID));
                Assert.Throws<DomainValidationException>(() => template.Archive(VALID_ACTOR_ID));
        }

        /// <summary>
        /// Verifies that <see cref="TemplateImageReference.Create"/> rejects empty template IDs and null references.
        /// </summary>
        [Fact]
        public void TemplateImageReferenceCreate_WhenInvalidArguments_ThrowsDomainValidationException()
        {
                // Arrange
                PictureReference validReference = new(VALID_NODE_ID, FIRST_IMAGE_ID);

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => TemplateImageReference.Create(Guid.Empty, validReference));
                Assert.Throws<DomainValidationException>(() => TemplateImageReference.Create(Guid.NewGuid(), null!));
        }

        private static TemplateEntity CreateValidTemplate()
        {
                string handle = new PictureReference(VALID_NODE_ID, FIRST_IMAGE_ID).ToString();
                return TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Initial Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"img\":\"{handle}\"}}",
                        actorId: VALID_ACTOR_ID);
        }
}

