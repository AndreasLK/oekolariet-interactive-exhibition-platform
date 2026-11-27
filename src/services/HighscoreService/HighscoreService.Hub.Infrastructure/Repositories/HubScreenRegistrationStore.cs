using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;
using HighscoreService.Hub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Hub.Infrastructure.Repositories
{
        /// <summary>
        /// SQL Server implementation of <see cref="IScreenRegistrationStore"/>.
        /// </summary>
        /// <param name="dbContext">Hub database context.</param>
        public sealed class HubScreenRegistrationStore(HubHighscoreDbContext dbContext) : IScreenRegistrationStore
        {
                private readonly HubHighscoreDbContext _dbContext = dbContext;

                /// <inheritdoc />
                public async Task<ScreenEnrollmentOutcome> EnrollIdempotentAsync(
                        ScreenRegistration registration,
                        int maxPendingCount,
                        CancellationToken cancellationToken)
                {
                        ScreenRegistration? existing = await this._dbContext.ScreenRegistrations
                                .FirstOrDefaultAsync(s => s.ScreenId == registration.ScreenId, cancellationToken);

                        if (existing is not null)
                        {
                                // WHY: Return existing registration without overwriting SecretHash or Status
                                // so an unauthenticated re-enroll call cannot hijack or reset an approved screen.
                                return new ScreenEnrollmentOutcome(existing, WasCreated: false, PendingCapReached: false);
                        }

                        int pendingCount = await this._dbContext.ScreenRegistrations
                                .CountAsync(s => s.Status == ScreenRegistrationStatus.Pending, cancellationToken);

                        if (pendingCount >= maxPendingCount)
                        {
                                return new ScreenEnrollmentOutcome(Registration: null, WasCreated: false, PendingCapReached: true);
                        }

                        this._dbContext.ScreenRegistrations.Add(registration);
                        await this._dbContext.SaveChangesAsync(cancellationToken);

                        return new ScreenEnrollmentOutcome(registration, WasCreated: true, PendingCapReached: false);
                }

                /// <inheritdoc />
                public async Task<ScreenRegistration?> GetByIdAsync(Guid screenId, CancellationToken cancellationToken)
                {
                        return await this._dbContext.ScreenRegistrations
                                .AsNoTracking()
                                .FirstOrDefaultAsync(s => s.ScreenId == screenId, cancellationToken);
                }

                /// <inheritdoc />
                public async Task<IReadOnlyList<ScreenRegistration>> ListAsync(CancellationToken cancellationToken)
                {
                        return await this._dbContext.ScreenRegistrations
                                .AsNoTracking()
                                .OrderByDescending(s => s.CreatedAtUtc)
                                .ToListAsync(cancellationToken);
                }

                /// <inheritdoc />
                public async Task<ScreenRegistration?> ApproveAsync(
                        Guid screenId,
                        DateTimeOffset approvedAtUtc,
                        CancellationToken cancellationToken)
                {
                        ScreenRegistration? registration = await this._dbContext.ScreenRegistrations
                                .FirstOrDefaultAsync(s => s.ScreenId == screenId, cancellationToken);

                        if (registration is null)
                        {
                                return null;
                        }

                        registration.Approve(approvedAtUtc);
                        await this._dbContext.SaveChangesAsync(cancellationToken);
                        return registration;
                }

                /// <inheritdoc />
                public async Task<ScreenRegistration?> RevokeAsync(
                        Guid screenId,
                        DateTimeOffset revokedAtUtc,
                        CancellationToken cancellationToken)
                {
                        ScreenRegistration? registration = await this._dbContext.ScreenRegistrations
                                .FirstOrDefaultAsync(s => s.ScreenId == screenId, cancellationToken);

                        if (registration is null)
                        {
                                return null;
                        }

                        registration.Revoke(revokedAtUtc);
                        await this._dbContext.SaveChangesAsync(cancellationToken);
                        return registration;
                }
        }
}

