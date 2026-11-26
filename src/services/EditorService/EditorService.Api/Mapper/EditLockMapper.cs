using EditorService.Facade.Commands.EditLocks;
using EditorService.Facade.DTOs.EditLocks;
using EditorService.Sdk.EditLocks.Responses;
using EditorService.Sdk.EditLocks.Reuests;

namespace EditorService.Api.Mapper
{
        /// <summary>
        /// Translates edit lock requests and responses between the Sdk (outside) and the Facade (inside).
        /// </summary>
        public static class EditLockMapper
        {
                /// <summary>Turns the request from the editor into a command for the handler.</summary>
                /// <param name="request">The device the employee is using.</param>
                /// <param name="gameId">The game to lock, from the URL.</param>
                /// <param name="holderName">The logged-in employee, from the JWT. Never from the client.</param>
                /// <returns>The command.</returns>
                public static AcquireLockCommand ToCommand(AcquireLockRequest request, Guid gameId, string holderName)
                {
                        return new AcquireLockCommand(GameId: gameId, HolderName: holderName, DeviceName: request.DeviceName);
                }

                /// <summary>Turns the lock from the handler into a response for the editor.</summary>
                /// <param name="dto">The lock from the handler.</param>
                /// <returns>The response with the lock token and expiry time.</returns>
                public static EditLockResponse ToResponse(EditLockDto dto)
                {
                        return new EditLockResponse(LockToken: dto.LockToken, ExpiresAt: dto.ExpiresAt);
                }
        }
}
