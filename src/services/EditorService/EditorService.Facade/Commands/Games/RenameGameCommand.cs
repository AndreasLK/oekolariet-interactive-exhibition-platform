using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.Games
{
        public sealed record RenameGameCommand(Guid GameId, Guid LockToken, string Title, string ActorId);
}
