using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// In-memory <see cref="IScreenRegistrationStore"/> test double for authentication handler unit tests.
        /// </summary>
        internal sealed class StubScreenRegistrationStore : IScreenRegistrationStore
        {
                private readonly Dictionary<Guid, ScreenRegistration> _registrations = [];

                /// <summary>
                /// Seeds a registration into the in-memory lookup dictionary.
                /// </summary>
                /// <param name="registration">Registration to store.</param>
                public void Add(ScreenRegistration registration)
                {
                        // Key by ScreenId to mirror primary-key lookup in SQL Server.
                        this._registrations[registration.ScreenId] = registration;
                }

                /// <inheritdoc />
                public Task<ScreenEnrollmentOutcome> EnrollIdempotentAsync(
                        ScreenRegistration registration,
                        int maxPendingCount,
                        CancellationToken cancellationToken)
                {
                        // Store and report creation for simple stub scenarios.
                        this._registrations[registration.ScreenId] = registration;
                        return Task.FromResult(new ScreenEnrollmentOutcome(registration, WasCreated: true, PendingCapReached: false));
                }

                /// <inheritdoc />
                public Task<ScreenRegistration?> GetByIdAsync(Guid screenId, CancellationToken cancellationToken)
                {
                        // Return matching registration or null if unknown.
                        this._registrations.TryGetValue(screenId, out ScreenRegistration? found);
                        return Task.FromResult(found);
                }

                /// <inheritdoc />
                public Task<IReadOnlyList<ScreenRegistration>> ListAsync(CancellationToken cancellationToken)
                {
                        // Return all seeded registrations.
                        return Task.FromResult<IReadOnlyList<ScreenRegistration>>([.. this._registrations.Values]);
                }

                /// <inheritdoc />
                public Task<ScreenRegistration?> ApproveAsync(
                        Guid screenId,
                        DateTimeOffset approvedAtUtc,
                        CancellationToken cancellationToken)
                {
                        // Approve in-memory if present.
                        if (!this._registrations.TryGetValue(screenId, out ScreenRegistration? found))
                        {
                                return Task.FromResult<ScreenRegistration?>(null);
                        }

                        found.Approve(approvedAtUtc);
                        return Task.FromResult<ScreenRegistration?>(found);
                }

                /// <inheritdoc />
                public Task<ScreenRegistration?> RevokeAsync(
                        Guid screenId,
                        DateTimeOffset revokedAtUtc,
                        CancellationToken cancellationToken)
                {
                        // Revoke in-memory if present.
                        if (!this._registrations.TryGetValue(screenId, out ScreenRegistration? found))
                        {
                                return Task.FromResult<ScreenRegistration?>(null);
                        }

                        found.Revoke(revokedAtUtc);
                        return Task.FromResult<ScreenRegistration?>(found);
                }
        }
}

