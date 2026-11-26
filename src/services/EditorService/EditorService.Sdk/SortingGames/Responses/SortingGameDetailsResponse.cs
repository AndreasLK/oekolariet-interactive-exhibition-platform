using EditorService.Sdk.SortingGames.Responses.CategoryItems;
using EditorService.Sdk.SortingGames.Responses.CategoryRecievers;

namespace EditorService.Sdk.SortingGames.Responses
{
        /// <summary>
        /// A sorting game with everything the editor needs to show it.
        /// </summary>
        /// <param name="Id">The id of the game.</param>
        /// <param name="Title">The title of the game.</param>
        /// <param name="Status">Draft or Published.</param>
        /// <param name="TemplateId">The chosen template, or null.</param>
        /// <param name="ThemeId">The chosen theme, or null.</param>
        /// <param name="CategoryReceivers">All categories in the game.</param>
        /// <param name="CategoryItems">All items in the game. Each item points to its category with CategoryReceiverId.</param>
        public sealed record SortingGameDetailsResponse(
                Guid Id,
                string Title,
                string Status,
                Guid? TemplateId,
                Guid? ThemeId,
                IReadOnlyList<CategoryReceiverResponse> CategoryReceivers,
                IReadOnlyList<CategoryItemResponse> CategoryItems);
}
