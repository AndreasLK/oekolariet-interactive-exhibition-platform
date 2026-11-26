using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Extensions
{
        /// <summary>
        /// Shared helpers on top of <see cref="IGameRepository"/>.
        /// </summary>
        public static class GameRepositoryExtensions
        {
                /// <summary>
                /// Gets a game and checks that it is a sorting game.
                /// </summary>
                /// <param name="gameRepository">The repository to get the game from.</param>
                /// <param name="gameId">The id of the game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The sorting game.</returns>
                /// <exception cref="GameNotFoundException">The game does not exist (404).</exception>
                /// <exception cref="DomainValidationException">The game exists but is not a sorting game (400).</exception>
                public static async Task<SortingGameEntity> GetSortingGameAsync(this IGameRepository gameRepository, Guid gameId, CancellationToken cancellationToken)
                {
                        // 1. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await gameRepository.GetByIdAsync(id: gameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: gameId);

                        // 2. Categories and items only exist on sorting games, e.g. not on a quiz
                        if (game is not SortingGameEntity sortingGame)
                        {
                                throw new DomainValidationException(message: "The game is not a sorting game.");
                        }

                        return sortingGame;
                }
        }
}
