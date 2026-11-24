using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.Games
{
        public sealed record DeleteGameCommand(Guid GameId, Guid LockToken, string ActorId);
}
