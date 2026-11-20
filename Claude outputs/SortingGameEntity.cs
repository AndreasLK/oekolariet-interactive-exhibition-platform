using EditorService.Domain.Enums;
using EditorService.Domain.Exceptions;

namespace EditorService.Domain.Entities
{
        /// <summary>
        /// A drag-and-drop game where the player sorts items into category receivers.
        /// Aggregate root: category receivers and category items are only added and changed through this class.
        /// </summary>
        public sealed class SortingGameEntity : GameEntity
        {
                // The category receivers and category items belong to this game. Only the game itself can add or remove them.
                private readonly List<CategoryReceiverEntity> _categoryReceivers = [];
                private readonly List<CategoryItemEntity> _categoryItems = [];

                /// <summary>
                /// Initializes a new instance of the <see cref="SortingGameEntity"/> class for EF Core.
                /// Use <see cref="Create"/> to make a new sorting game.
                /// </summary>
                private SortingGameEntity()
                {
                }

                /// <summary>Gets the kind of game, which is always a sorting game.</summary>
                public override GameType Type => GameType.SortingGame;

                /// <summary>Gets the category receivers in the game. Read-only from the outside.</summary>
                public IReadOnlyList<CategoryReceiverEntity> CategoryReceivers => this._categoryReceivers;

                /// <summary>Gets the category items in the game. Read-only from the outside.</summary>
                public IReadOnlyList<CategoryItemEntity> CategoryItems => this._categoryItems;

                /// <summary>
                /// Creates a new sorting game as a draft.
                /// </summary>
                /// <param name="title">The title of the game.</param>
                /// <param name="actorId">The identifier of the actor creating the game.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>A new <see cref="SortingGameEntity"/>.</returns>
                public static SortingGameEntity Create(string title, string actorId, DateTimeOffset now)
                {
                        SortingGameEntity game = new();
                        game.InitializeGame(title: title, actorId: actorId, now: now);
                        return game;
                }

                /// <summary>
                /// Checks that the sorting game is complete before it can be published.
                /// </summary>
                protected override void EnsureReadyToPublish()
                {
                        // A sorting game needs at least one category receiver and one category item before it can be shown on a screen.
                        if (this._categoryReceivers.Count == 0)
                        {
                                throw new DomainValidationException(message: "A sorting game must have at least one category receiver before it can be published.");
                        }

                        if (this._categoryItems.Count == 0)
                        {
                                throw new DomainValidationException(message: "A sorting game must have at least one category item before it can be published.");
                        }
                }
        }
}
