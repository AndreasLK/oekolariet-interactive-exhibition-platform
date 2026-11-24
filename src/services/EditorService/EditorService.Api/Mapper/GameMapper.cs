using EditorService.Facade.Commands.Games;
using EditorService.Facade.DTOs.Games;
using EditorService.Sdk.Games.Requests;
using EditorService.Sdk.Games.Responses;

namespace EditorService.Api.Mapper
{
        /// <summary>
        /// Translates between the Sdk (outside) and the Facade (inside).
        /// </summary>
        public static class GameMapper
        {
                /// <summary>Turns the request from the editor into a command for the handler.</summary>
                /// <param name="request">The request from the editor.</param>
                /// <param name="actorId">The logged-in employee.</param>
                /// <returns>The command.</returns>
                public static CreateSortingGameCommand ToCommand(CreateSortingGameRequest request, string actorId)
                {
                        return new CreateSortingGameCommand(Title: request.Title, ActorId: actorId);
                }

                /// <summary>Turns the DTO from the handler into a response for the editor.</summary>
                /// <param name="dto">The DTO from the handler.</param>
                /// <returns>The response.</returns>
                public static GameResponse ToResponse(GameDto dto)
                {
                        return new GameResponse(Id: dto.Id, Title: dto.Title, Type: dto.Type, Status: dto.Status);
                }
        }
}
