using EditorService.Facade.DTOs.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.Queries.Games
{
        public interface IGameQueries
        {
                public Task<GameDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
                public Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken);
        }
}
