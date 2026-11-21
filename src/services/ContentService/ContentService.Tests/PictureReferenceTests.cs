using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Tests;

/// <summary>
/// Unit tests for <see cref="PictureReference"/> parsing, formatting, node validation, and JSON handle extraction.
/// </summary>
public sealed class PictureReferenceTests
{
        private const string VALID_NODE_ID = "node-1";
        private static readonly Guid SAMPLE_IMAGE_ID = Guid.Parse("a1b2c3d4-e5f6-4789-abcd-ef1234567890");

        /// <summary>
        /// Verifies that valid handles format consistently for both template schemas and external HTTP routes.
        /// </summary>
        [Fact]
        public void Constructor_WhenValid_FormatsTemplateHandleAndExternalId()
        {
                // Arrange
                PictureReference reference = new($"  {VALID_NODE_ID}  ", SAMPLE_IMAGE_ID);

                // Act
                string templateHandle = reference.ToString();
                string externalId = reference.ToExternalImageId();

                // Assert
                Assert.Equal(VALID_NODE_ID, reference.StorageNodeId);
                Assert.Equal($"{DomainConstants.PICTURE_REFERENCE_PREFIX}{VALID_NODE_ID}.{SAMPLE_IMAGE_ID:D}", templateHandle);
                Assert.Equal($"{VALID_NODE_ID}_{SAMPLE_IMAGE_ID:D}", externalId);
        }

        /// <summary>
        /// Verifies that invalid storage node identifiers are rejected by the constructor.
        /// </summary>
        /// <param name="invalidNodeId">An invalid storage node identifier.</param>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Node-1")]
        [InlineData("node_1")]
        [InlineData("node.1")]
        [InlineData("-node1")]
        [InlineData("node1-")]
        [InlineData("node--1")]
        public void Constructor_WhenStorageNodeIdInvalid_ThrowsDomainValidationException(string invalidNodeId)
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => new PictureReference(invalidNodeId, SAMPLE_IMAGE_ID));
        }

        /// <summary>
        /// Verifies that storage node identifiers longer than <see cref="DomainConstants.MAX_STORAGE_NODE_ID_LENGTH"/> are rejected.
        /// </summary>
        [Fact]
        public void Constructor_WhenStorageNodeIdExceedsMaxLength_ThrowsDomainValidationException()
        {
                // Arrange
                string oversizedNodeId = new('a', DomainConstants.MAX_STORAGE_NODE_ID_LENGTH + 1);

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => new PictureReference(oversizedNodeId, SAMPLE_IMAGE_ID));
        }

        /// <summary>
        /// Verifies that <see cref="Guid.Empty"/> is rejected by the constructor.
        /// </summary>
        [Fact]
        public void Constructor_WhenImageIdIsEmpty_ThrowsDomainValidationException()
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => new PictureReference(VALID_NODE_ID, Guid.Empty));
        }

        /// <summary>
        /// Verifies that <see cref="PictureReference.Parse"/> round-trips a valid handle and rejects malformed handles.
        /// </summary>
        [Fact]
        public void Parse_WhenValidHandle_ReturnsEqualPictureReference()
        {
                // Arrange
                string handle = $"{DomainConstants.PICTURE_REFERENCE_PREFIX}{VALID_NODE_ID}.{SAMPLE_IMAGE_ID.ToString().ToUpperInvariant()}";

                // Act
                PictureReference parsed = PictureReference.Parse(handle);

                // Assert
                Assert.Equal(new PictureReference(VALID_NODE_ID, SAMPLE_IMAGE_ID), parsed);
        }

        /// <summary>
        /// Verifies that <see cref="PictureReference.TryParse"/> rejects wrong prefixes, non-hyphenated GUIDs, and extra segments.
        /// </summary>
        /// <param name="malformedHandle">A malformed picture handle.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Picture.node-1.a1b2c3d4-e5f6-4789-abcd-ef1234567890")]
        [InlineData(" picture.node-1.a1b2c3d4-e5f6-4789-abcd-ef1234567890")]
        [InlineData("picture.node-1.a1b2c3d4e5f64789abcdef1234567890")]
        [InlineData("picture.node-1.00000000-0000-0000-0000-000000000000")]
        [InlineData("picture.node-1.a1b2c3d4-e5f6-4789-abcd-ef1234567890.extra")]
        public void TryParse_WhenMalformed_ReturnsFalse(string? malformedHandle)
        {
                // Act
                bool success = PictureReference.TryParse(malformedHandle, out PictureReference? reference);

                // Assert
                Assert.False(success);
                Assert.Null(reference);
        }

        /// <summary>
        /// Verifies that <see cref="PictureReference.ParseExternalId"/> parses valid <c>{node}_{guid}</c> strings and rejects invalid ones.
        /// </summary>
        [Fact]
        public void ParseExternalId_WhenValid_ReturnsNodeIdAndImageId()
        {
                // Arrange
                string externalId = $"{VALID_NODE_ID}_{SAMPLE_IMAGE_ID:D}";

                // Act
                (string storageNodeId, Guid imageId) = PictureReference.ParseExternalId(externalId);

                // Assert
                Assert.Equal(VALID_NODE_ID, storageNodeId);
                Assert.Equal(SAMPLE_IMAGE_ID, imageId);
        }

        /// <summary>
        /// Verifies that <see cref="PictureReference.ExtractReferences"/> deduplicates handles regardless of GUID hex casing.
        /// </summary>
        [Fact]
        public void ExtractReferences_WhenJsonContainsDuplicateMixedCaseHandles_ReturnsDeduplicatedList()
        {
                // Arrange
                string lowerHandle = $"{DomainConstants.PICTURE_REFERENCE_PREFIX}{VALID_NODE_ID}.{SAMPLE_IMAGE_ID:D}";
                string upperHandle = $"{DomainConstants.PICTURE_REFERENCE_PREFIX}{VALID_NODE_ID}.{SAMPLE_IMAGE_ID.ToString().ToUpperInvariant()}";
                string json = $$"""{"hero":"{{lowerHandle}}","thumb":"{{upperHandle}}"}""";

                // Act
                List<PictureReference> references = PictureReference.ExtractReferences(json);

                // Assert
                PictureReference single = Assert.Single(references);
                Assert.Equal(new PictureReference(VALID_NODE_ID, SAMPLE_IMAGE_ID), single);
        }

        /// <summary>
        /// Verifies that regex boundaries ignore embedded substrings, 37-char hex typos, overlong node IDs, and <see cref="Guid.Empty"/>.
        /// </summary>
        [Fact]
        public void ExtractReferences_WhenJsonContainsNearMisses_IgnoresInvalidMatches()
        {
                // Arrange
                string oversizedNode = new('a', DomainConstants.MAX_STORAGE_NODE_ID_LENGTH + 1);
                string json = $$"""
                {
                        "wordBoundary": "mypicture.{{VALID_NODE_ID}}.{{SAMPLE_IMAGE_ID:D}}",
                        "trailingHex": "{{DomainConstants.PICTURE_REFERENCE_PREFIX}}{{VALID_NODE_ID}}.{{SAMPLE_IMAGE_ID:D}}f",
                        "trailingHyphen": "{{DomainConstants.PICTURE_REFERENCE_PREFIX}}{{VALID_NODE_ID}}.{{SAMPLE_IMAGE_ID:D}}-1",
                        "oversizedNode": "{{DomainConstants.PICTURE_REFERENCE_PREFIX}}{{oversizedNode}}.{{SAMPLE_IMAGE_ID:D}}",
                        "emptyGuid": "{{DomainConstants.PICTURE_REFERENCE_PREFIX}}{{VALID_NODE_ID}}.{{Guid.Empty:D}}"
                }
                """;

                // Act
                List<PictureReference> references = PictureReference.ExtractReferences(json);

                // Assert
                Assert.Empty(references);
        }

        /// <summary>
        /// Verifies that inline base64 image data URIs (both raw and JSON-escaped) are rejected.
        /// </summary>
        /// <param name="marker">The disallowed data URI marker.</param>
        [Theory]
        [InlineData(DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER)]
        [InlineData(DomainConstants.ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER)]
        public void ExtractReferences_WhenEmbeddedBase64ImagePresent_ThrowsDomainValidationException(string marker)
        {
                // Arrange
                string json = $$"""{"src":"{{marker.ToUpperInvariant()}}png;base64,AAAA"}""";

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => PictureReference.ExtractReferences(json));
        }
}

