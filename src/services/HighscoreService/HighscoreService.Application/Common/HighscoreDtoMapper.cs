using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Common
{
        /// <summary>
        /// Maps between <see cref="Domain"/> entities/specifications and <see cref="Facade"/> DTOs.
        /// </summary>
        public static class HighscoreDtoMapper
        {
                /// <summary>
                /// Maps a <see cref="HighscoreEntity"/> to a <see cref="HighscoreDto"/>.
                /// </summary>
                /// <param name="entity">Domain entity.</param>
                /// <returns>Mapped DTO.</returns>
                public static HighscoreDto ToDto(HighscoreEntity entity)
                {
                        // Project domain properties directly onto the immutable transport DTO.
                        return new HighscoreDto(
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

                /// <summary>
                /// Maps a <see cref="HighscoreQueryDto"/> to a <see cref="HighscoreSpecification"/>.
                /// </summary>
                /// <param name="query">Facade query DTO.</param>
                /// <returns>Domain query specification.</returns>
                public static HighscoreSpecification ToSpecification(HighscoreQueryDto query)
                {
                        // Always exclude soft-deleted rows from public/standard query specifications.
                        return new HighscoreSpecification
                        {
                                GameId = query.GameId,
                                SenderMacAddress = query.SenderMacAddress,
                                FromUtc = query.FromUtc,
                                ToUtc = query.ToUtc,
                                LastHours = query.LastHours,
                                MinAge = query.MinAge,
                                MaxAge = query.MaxAge,
                                IncludeUnnamed = query.IncludeUnnamed,
                                IncludeTimedOut = query.IncludeTimedOut,
                                IncludeDeleted = false,
                                Page = query.Page,
                                PageSize = query.PageSize
                        };
                }

                /// <summary>
                /// Reconstructs a validated <see cref="HighscoreEntity"/> from an incoming sync <see cref="HighscoreDto"/>.
                /// </summary>
                /// <param name="dto">Incoming sync DTO.</param>
                /// <returns>Validated domain entity.</returns>
                public static HighscoreEntity ToDomainEntityForSync(HighscoreDto dto)
                {
                        // Re-validate all domain invariants when hydrating entries received over sync.
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: dto.Id,
                                name: dto.Name,
                                age: dto.Age,
                                score: dto.Score,
                                senderMacAddress: dto.SenderMacAddress,
                                gameId: dto.GameId,
                                playedAtUtc: dto.PlayedAtUtc,
                                isTimedOut: dto.IsTimedOut,
                                rankingAtTimeOfScoring: dto.RankingAtTimeOfScoring);

                        // Preserve soft-delete state so tombstoned records remain deleted locally.
                        if (dto.IsDeleted && dto.DeletedAtUtc.HasValue)
                        {
                                entity.MarkDeleted(dto.DeletedAtUtc.Value);
                        }

                        return entity;
                }
        }
}

