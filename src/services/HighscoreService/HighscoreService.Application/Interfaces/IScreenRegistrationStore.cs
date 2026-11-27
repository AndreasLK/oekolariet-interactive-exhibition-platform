using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.Interfaces
{
        /// <summary>
        /// Persistence abstraction for Hub-side screen registrations and approval states.
        /// </summary>
        public interface IScreenRegistrationStore
        {
                /// <summary>
                /// Enrolls a screen idempotently in <see cref="ScreenRegistrationStatus.Pending"/> state,
                /// enforcing a cap on unapproved pending registrations to mitigate flooding.
                /// </summary>
                /// <param name="registration">Validated screen registration.</param>
                /// <param name="maxPendingCount">Maximum allowed pending rows before new enrollments are rejected.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Enrollment outcome.</returns>
                public Task<ScreenEnrollmentOutcome> EnrollIdempotentAsync(
                        ScreenRegistration registration,
                        int maxPendingCount,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Retrieves a screen registration by its identifier.
                /// </summary>
                /// <param name="screenId">Screen identifier.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The matching registration, or <see langword="null"/> if not found.</returns>
                public Task<ScreenRegistration?> GetByIdAsync(Guid screenId, CancellationToken cancellationToken);

                /// <summary>
                /// Lists all registered screens ordered by creation time descending.
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>All screen registrations.</returns>
                public Task<IReadOnlyList<ScreenRegistration>> ListAsync(CancellationToken cancellationToken);

                /// <summary>
                /// Approves an enrolled screen.
                /// </summary>
                /// <param name="screenId">Screen identifier.</param>
                /// <param name="approvedAtUtc">Approval timestamp.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Updated registration, or <see langword="null"/> if not found.</returns>
                public Task<ScreenRegistration?> ApproveAsync(Guid screenId, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken);

                /// <summary>
                /// Revokes an enrolled screen immediately.
                /// </summary>
                /// <param name="screenId">Screen identifier.</param>
                /// <param name="revokedAtUtc">Revocation timestamp.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Updated registration, or <see langword="null"/> if not found.</returns>
                public Task<ScreenRegistration?> RevokeAsync(Guid screenId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken);
        }
}

