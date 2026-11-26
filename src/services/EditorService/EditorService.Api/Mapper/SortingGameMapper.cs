using EditorService.Facade.Commands.SortingGames.CategoryItems;
using EditorService.Facade.Commands.SortingGames.CategoryRecievers;
using EditorService.Facade.DTOs.SortingGames;
using EditorService.Facade.DTOs.SortingGames.CategoryItems;
using EditorService.Facade.DTOs.SortingGames.CategoryRecievers;
using EditorService.Sdk.SortingGames.Requests.CategoryItems;
using EditorService.Sdk.SortingGames.Requests.CategoryRecievers;
using EditorService.Sdk.SortingGames.Responses;
using EditorService.Sdk.SortingGames.Responses.CategoryItems;
using EditorService.Sdk.SortingGames.Responses.CategoryRecievers;

namespace EditorService.Api.Mapper
{
        /// <summary>
        /// Translates sorting game requests and responses between the Sdk (outside) and the Facade (inside).
        /// </summary>
        public static class SortingGameMapper
        {
                // ---------- Requests → commands ----------

                /// <summary>Turns an add category request into a command.</summary>
                /// <param name="request">The name, color and image from the editor.</param>
                /// <param name="gameId">The sorting game, from the URL.</param>
                /// <param name="lockToken">The edit lock token, from the X-Lock-Token header.</param>
                /// <param name="actorId">The logged-in employee.</param>
                /// <returns>The command.</returns>
                public static AddCategoryReceiverCommand ToCommand(AddCategoryReceiverRequest request, Guid gameId, Guid lockToken, string actorId)
                {
                        return new AddCategoryReceiverCommand(GameId: gameId, LockToken: lockToken, Name: request.Name, Color: request.Color, ImageId: request.ImageId, ActorId: actorId);
                }

                /// <summary>Turns an update category request into a command.</summary>
                /// <param name="request">The new name, color and image from the editor.</param>
                /// <param name="gameId">The sorting game, from the URL.</param>
                /// <param name="receiverId">The category, from the URL.</param>
                /// <param name="lockToken">The edit lock token, from the X-Lock-Token header.</param>
                /// <param name="actorId">The logged-in employee.</param>
                /// <returns>The command.</returns>
                public static UpdateCategoryReceiverCommand ToCommand(UpdateCategoryReceiverRequest request, Guid gameId, Guid receiverId, Guid lockToken, string actorId)
                {
                        return new UpdateCategoryReceiverCommand(GameId: gameId, CategoryReceiverId: receiverId, LockToken: lockToken, Name: request.Name, Color: request.Color, ImageId: request.ImageId, ActorId: actorId);
                }

                /// <summary>Turns an add item request into a command.</summary>
                /// <param name="request">The category, name, explanation and image from the editor.</param>
                /// <param name="gameId">The sorting game, from the URL.</param>
                /// <param name="lockToken">The edit lock token, from the X-Lock-Token header.</param>
                /// <param name="actorId">The logged-in employee.</param>
                /// <returns>The command.</returns>
                public static AddCategoryItemCommand ToCommand(AddCategoryItemRequest request, Guid gameId, Guid lockToken, string actorId)
                {
                        return new AddCategoryItemCommand(GameId: gameId, LockToken: lockToken, CategoryReceiverId: request.CategoryReceiverId, Name: request.Name, Explanation: request.Explanation, ImageId: request.ImageId, ActorId: actorId);
                }

                /// <summary>Turns an update item request into a command.</summary>
                /// <param name="request">The new name, explanation and image from the editor.</param>
                /// <param name="gameId">The sorting game, from the URL.</param>
                /// <param name="itemId">The item, from the URL.</param>
                /// <param name="lockToken">The edit lock token, from the X-Lock-Token header.</param>
                /// <param name="actorId">The logged-in employee.</param>
                /// <returns>The command.</returns>
                public static UpdateCategoryItemCommand ToCommand(UpdateCategoryItemRequest request, Guid gameId, Guid itemId, Guid lockToken, string actorId)
                {
                        return new UpdateCategoryItemCommand(GameId: gameId, CategoryItemId: itemId, LockToken: lockToken, Name: request.Name, Explanation: request.Explanation, ImageId: request.ImageId, ActorId: actorId);
                }

                // ---------- DTOs → responses ----------

                /// <summary>Turns a category from the handler into a response for the editor.</summary>
                /// <param name="dto">The category.</param>
                /// <returns>The response.</returns>
                public static CategoryReceiverResponse ToResponse(CategoryReceiverDto dto)
                {
                        return new CategoryReceiverResponse(Id: dto.Id, Name: dto.Name, Color: dto.Color, ImageId: dto.ImageId);
                }

                /// <summary>Turns an item from the handler into a response for the editor.</summary>
                /// <param name="dto">The item.</param>
                /// <returns>The response.</returns>
                public static CategoryItemResponse ToResponse(CategoryItemDto dto)
                {
                        return new CategoryItemResponse(Id: dto.Id, Name: dto.Name, Explanation: dto.Explanation, ImageId: dto.ImageId, CategoryReceiverId: dto.CategoryReceiverId);
                }

                /// <summary>Turns a whole sorting game from the queries into a response for the editor.</summary>
                /// <param name="dto">The sorting game with its categories and items.</param>
                /// <returns>The response.</returns>
                public static SortingGameDetailsResponse ToResponse(SortingGameDetailsDto dto)
                {
                        return new SortingGameDetailsResponse(
                                Id: dto.Id,
                                Title: dto.Title,
                                Status: dto.Status,
                                TemplateId: dto.TemplateId,
                                ThemeId: dto.ThemeId,
                                // Each category and item uses the two ToResponse methods above.
                                CategoryReceivers: dto.CategoryReceivers.Select(selector: ToResponse).ToList(),
                                CategoryItems: dto.CategoryItems.Select(selector: ToResponse).ToList());
                }
        }
}
