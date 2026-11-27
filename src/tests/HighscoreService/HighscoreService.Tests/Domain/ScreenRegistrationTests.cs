using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Exceptions;

namespace HighscoreService.Tests.Domain
{
        /// <summary>
        /// Unit tests for <see cref="ScreenRegistration"/> enrollment validation, hashing, and status transitions.
        /// </summary>
        public sealed class ScreenRegistrationTests
        {
                /// <summary>
                /// Verifies valid enrollment trims node ID, normalizes MAC, hashes secret, and starts in Pending state.
                /// </summary>
                [Fact]
                public void Enroll_ValidInput_StartsPendingWithNormalizedFields()
                {
                        // Arrange & Act
                        DateTimeOffset createdAt = new(2026, 11, 27, 10, 0, 0, TimeSpan.FromHours(1));
                        ScreenRegistration registration = ScreenRegistration.Enroll(
                                screenId: Guid.CreateVersion7(),
                                nodeId: "  kiosk-north  ",
                                macAddress: "aa-bb-cc-dd-ee-01",
                                rawSecret: "super-secret-at-least-16-chars",
                                createdAtUtc: createdAt);

                        // Assert
                        Assert.Equal("kiosk-north", registration.NodeId);
                        Assert.Equal("AA:BB:CC:DD:EE:01", registration.MacAddress);
                        Assert.Equal(ScreenRegistrationStatus.Pending, registration.Status);
                        Assert.Equal(ScreenRegistration.SECRET_HASH_HEX_LENGTH, registration.SecretHash.Length);
                        Assert.Equal(TimeSpan.Zero, registration.CreatedAtUtc.Offset);
                        Assert.True(registration.VerifySecret("super-secret-at-least-16-chars"));
                }

                /// <summary>
                /// Verifies enrollment rejects empty screen identifiers, invalid node IDs, and weak secrets.
                /// </summary>
                [Fact]
                public void Enroll_InvalidInputs_ThrowDomainValidationException()
                {
                        // Reject empty ScreenId.
                        Assert.Throws<DomainValidationException>(() => ScreenRegistration.Enroll(
                                screenId: Guid.Empty,
                                nodeId: "kiosk-01",
                                macAddress: null,
                                rawSecret: "super-secret-at-least-16-chars",
                                createdAtUtc: DateTimeOffset.UtcNow));

                        // Reject blank or oversized NodeId.
                        Assert.Throws<DomainValidationException>(() => ScreenRegistration.Enroll(
                                screenId: Guid.CreateVersion7(),
                                nodeId: "   ",
                                macAddress: null,
                                rawSecret: "super-secret-at-least-16-chars",
                                createdAtUtc: DateTimeOffset.UtcNow));

                        string oversizedNodeId = new('a', ScreenRegistration.MAX_NODE_ID_LENGTH + 1);
                        Assert.Throws<DomainValidationException>(() => ScreenRegistration.Enroll(
                                screenId: Guid.CreateVersion7(),
                                nodeId: oversizedNodeId,
                                macAddress: null,
                                rawSecret: "super-secret-at-least-16-chars",
                                createdAtUtc: DateTimeOffset.UtcNow));

                        // Reject secrets shorter than 16 characters.
                        Assert.Throws<DomainValidationException>(() => ScreenRegistration.Enroll(
                                screenId: Guid.CreateVersion7(),
                                nodeId: "kiosk-01",
                                macAddress: null,
                                rawSecret: "too-short",
                                createdAtUtc: DateTimeOffset.UtcNow));
                }

                /// <summary>
                /// Verifies <see cref="ScreenRegistration.VerifySecret"/> rejects blank and mismatched secrets.
                /// </summary>
                [Fact]
                public void VerifySecret_ReturnsFalse_ForBlankOrWrongSecret()
                {
                        // Arrange
                        ScreenRegistration registration = ScreenRegistration.Enroll(
                                screenId: Guid.CreateVersion7(),
                                nodeId: "kiosk-01",
                                macAddress: null,
                                rawSecret: "correct-secret-1234567890",
                                createdAtUtc: DateTimeOffset.UtcNow);

                        // Assert
                        Assert.False(registration.VerifySecret(""));
                        Assert.False(registration.VerifySecret("   "));
                        Assert.False(registration.VerifySecret("wrong-secret-1234567890"));
                }

                /// <summary>
                /// Verifies <see cref="ScreenRegistration.Revoke"/> and re-<see cref="ScreenRegistration.Approve"/> transitions.
                /// </summary>
                [Fact]
                public void ApproveAndRevoke_UpdateStatusAndClearRevocationOnReApproval()
                {
                        // Arrange
                        DateTimeOffset t0 = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);
                        ScreenRegistration registration = ScreenRegistration.Enroll(
                                screenId: Guid.CreateVersion7(),
                                nodeId: "kiosk-01",
                                macAddress: null,
                                rawSecret: "correct-secret-1234567890",
                                createdAtUtc: t0);

                        // Act: Approve -> Revoke -> Re-approve.
                        registration.Approve(t0.AddMinutes(1));
                        Assert.Equal(ScreenRegistrationStatus.Approved, registration.Status);
                        Assert.Null(registration.RevokedAtUtc);

                        registration.Revoke(t0.AddMinutes(2));
                        Assert.Equal(ScreenRegistrationStatus.Revoked, registration.Status);
                        Assert.Equal(t0.AddMinutes(2), registration.RevokedAtUtc);

                        registration.Approve(t0.AddMinutes(3));

                        // Assert: Re-approval clears prior revocation timestamp.
                        Assert.Equal(ScreenRegistrationStatus.Approved, registration.Status);
                        Assert.Equal(t0.AddMinutes(3), registration.ApprovedAtUtc);
                        Assert.Null(registration.RevokedAtUtc);
                }
        }
}

