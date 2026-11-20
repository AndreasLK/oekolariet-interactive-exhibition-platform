using EditorService.Domain.Entities;
using EditorService.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Tests.TestDoubles
{
        /// <summary>
        /// A minimal game used only in tests, so the shared rules in <see cref="GameEntity"/>
        /// can be tested before SortingGameEntity and QuizEntity exist.
        /// </summary>
        public sealed class TestGame : GameEntity
        {
                /// <summary>
                /// Gets the kind of game. The value does not matter for these tests.
                /// </summary>
                public override GameType Type => GameType.Quiz;

                /// <summary>
                /// Creates a new test game with the given title.
                /// </summary>
                /// <param name="title">The title of the game.</param>
                /// <param name="now">The time the game is created.</param>
                /// <returns>A new <see cref="TestGame"/>.</returns>
                public static TestGame Create(string title, DateTimeOffset now)
                {
                        TestGame game = new();
                        game.InitializeGame(title: title, actorId: "test-user", now: now);
                        return game;
                }

                /// <summary>
                /// A test game is always ready to publish.
                /// </summary>
                protected override void EnsureReadyToPublish()
                {
                }
        }
}
