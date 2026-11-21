using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Tests;

/// <summary>
/// Unit tests for shared identity, audit, and optimistic concurrency behavior in <see cref="BaseEntity"/>.
/// </summary>
public sealed class BaseEntityTests
{
        private const string VALID_ACTOR_ID = "curator-1";

        /// <summary>
        /// Verifies that <see cref="BaseEntity.InitializeAudit"/> assigns a new ID, concurrency stamp, and trimmed actor fields.
        /// </summary>
        [Fact]
        public void InitializeAudit_WhenActorIdHasSurroundingWhitespace_TrimsAndSetsAuditFields()
        {
                // Arrange
                string paddedMaxActor = $"  {new string('a', DomainConstants.MAX_ACTOR_ID_LENGTH)}  ";

                // Act
                StubEntity entity = StubEntity.Create(paddedMaxActor);

                // Assert
                Assert.NotEqual(Guid.Empty, entity.Id);
                Assert.NotEqual(Guid.Empty, entity.ConcurrencyStamp);
                Assert.Equal(DomainConstants.MAX_ACTOR_ID_LENGTH, entity.CreatedBy.Length);
                Assert.Equal(entity.CreatedBy, entity.UpdatedBy);
                Assert.Equal(entity.CreatedAt, entity.UpdatedAt);
        }

        /// <summary>
        /// Verifies that <see cref="BaseEntity.InitializeAudit"/> rejects blank or oversized actor identifiers.
        /// </summary>
        /// <param name="invalidActorId">An invalid actor identifier to test.</param>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void InitializeAudit_WhenActorIdIsBlank_ThrowsDomainValidationException(string invalidActorId)
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => StubEntity.Create(invalidActorId));
        }

        /// <summary>
        /// Verifies that <see cref="BaseEntity.InitializeAudit"/> rejects actor identifiers exceeding <see cref="DomainConstants.MAX_ACTOR_ID_LENGTH"/>.
        /// </summary>
        [Fact]
        public void InitializeAudit_WhenActorIdExceedsMaxLength_ThrowsDomainValidationException()
        {
                // Arrange
                string oversizedActorId = new('a', DomainConstants.MAX_ACTOR_ID_LENGTH + 1);

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => StubEntity.Create(oversizedActorId));
        }

        /// <summary>
        /// Verifies that <see cref="BaseEntity.MarkModified"/> rotates <see cref="BaseEntity.ConcurrencyStamp"/> while preserving creation metadata.
        /// </summary>
        [Fact]
        public void MarkModified_WhenValidActor_RotatesConcurrencyStampAndPreservesCreationMetadata()
        {
                // Arrange
                StubEntity entity = StubEntity.Create(VALID_ACTOR_ID);
                Guid originalId = entity.Id;
                Guid originalStamp = entity.ConcurrencyStamp;
                DateTimeOffset originalCreatedAt = entity.CreatedAt;

                // Act
                entity.Touch("editor-2");

                // Assert
                Assert.Equal(originalId, entity.Id);
                Assert.Equal(originalCreatedAt, entity.CreatedAt);
                Assert.Equal(VALID_ACTOR_ID, entity.CreatedBy);
                Assert.Equal("editor-2", entity.UpdatedBy);
                Assert.NotEqual(originalStamp, entity.ConcurrencyStamp);
                Assert.NotEqual(Guid.Empty, entity.ConcurrencyStamp);
        }

        /// <summary>
        /// Minimal concrete subclass used to test protected <see cref="BaseEntity"/> behavior in isolation.
        /// </summary>
        private sealed class StubEntity : BaseEntity
        {
                public static StubEntity Create(string actorId)
                {
                        StubEntity entity = new();
                        entity.InitializeAudit(actorId);
                        return entity;
                }

                public void Touch(string actorId)
                {
                        this.MarkModified(actorId);
                }
        }
}

