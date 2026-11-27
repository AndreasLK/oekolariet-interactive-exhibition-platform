using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Outcome of an idempotent screen enrollment attempt.
        /// </summary>
        /// <param name="Registration">The existing or newly created registration, or <see langword="null"/> if rejected due to pending cap.</param>
        /// <param name="WasCreated"><see langword="true"/> if a new registration row was inserted.</param>
        /// <param name="PendingCapReached"><see langword="true"/> if enrollment was rejected because the pending queue is full.</param>
        public sealed record ScreenEnrollmentOutcome(
                ScreenRegistration? Registration,
                bool WasCreated,
                bool PendingCapReached);
}

