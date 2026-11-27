using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.Common
{
        /// <summary>
        /// Maps domain <see cref="HighscoreEntity"/> instances to application result models.
        /// </summary>
        public static class HighscoreResultMapper
        {
                /// <summary>
                /// Converts a <see cref="HighscoreEntity"/> to a <see cref="HighscoreResult"/>.
                /// </summary>
                /// <param name="entity">The domain entity to map.</param>
                /// <returns>The mapped <see cref="HighscoreResult"/>.</returns>
                public static HighscoreResult ToResult(HighscoreEntity entity)
                {
                        // Project domain fields into the application-layer sync/report result record.
                        return new HighscoreResult(
                                Id: entity.Id,
                                Name: entity.Name,
                                Age: entity.Age,
                                Score: entity.Score,
                                SenderMacAddress: entity.SenderMacAddress,
                                GameId: entity.GameId,
                                PlayedAtUtc: entity.PlayedAtUtc,
                                RankingAtTimeOfScoring: entity.RankingAtTimeOfScoring,
                                IsTimedOut: entity.IsTimedOut,
                                IsDeleted: entity.IsDeleted,
                                DeletedAtUtc: entity.DeletedAtUtc,
                                SyncSequence: entity.SyncSequence);
                }
        }
}

