using Asp.Versioning;
using EditorService.Api.Mapper;
using EditorService.Application.Interfaces.CommandHandlers.EditLocks;
using EditorService.Facade.Commands.EditLocks;
using EditorService.Facade.DTOs.EditLocks;
using EditorService.Sdk.EditLocks.Responses;
using EditorService.Sdk.EditLocks.Reuests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace EditorService.Api.Controllers.V1
{
        /// <summary>
        /// Endpoints for the edit lock on a game.
        /// Only one employee can edit a game at a time. The lock token from here
        /// is sent in the X-Lock-Token header on every change in <see cref="GamesController"/>.
        /// </summary>
        [ApiController]
        [ApiVersion(version: 1.0)]
        [Route(template: "api/v{version:apiVersion}/games/{gameId:guid}/lock")]
        public sealed class EditLocksController : EditorControllerBase
        {
                private readonly IAcquireLockCommandHandler _acquireLock;
                private readonly IRenewLockCommandHandler _renewLock;
                private readonly IReleaseLockCommandHandler _releaseLock;

                /// <summary>
                /// Initializes a new instance of the <see cref="EditLocksController"/> class.
                /// </summary>
                /// <param name="acquireLock">Takes the lock on a game.</param>
                /// <param name="renewLock">Extends the lock.</param>
                /// <param name="releaseLock">Gives the lock free.</param>
                public EditLocksController(IAcquireLockCommandHandler acquireLock, IRenewLockCommandHandler renewLock, IReleaseLockCommandHandler releaseLock)
                {
                        this._acquireLock = acquireLock;
                        this._renewLock = renewLock;
                        this._releaseLock = releaseLock;
                }

                /// <summary>
                /// Takes the edit lock on a game.
                /// When an employee opens a game in the editor, the frontend calls this endpoint first.
                /// The holder name is the logged-in employee, so nobody can take a lock in someone else's name.
                /// If the employee already holds the lock on the same device, it is renewed and the same token is returned.
                /// </summary>
                /// <remarks>
                /// 200 with the lock token. 400 when the device name is not valid.
                /// 404 when the game does not exist. 423 when another employee holds the lock.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="request">The device the employee is using.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>The lock token and when the lock expires.</returns>
                [HttpPost(template: "")]
                [EndpointSummary(summary: "Låser et spil til redigering")]
                [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status423Locked)]
                public async Task<ActionResult<EditLockResponse>> AcquireLockAsync(Guid gameId, AcquireLockRequest request, CancellationToken cancellationToken)
                {
                        EditLockDto editLock = await this._acquireLock.HandleAsync(command: EditLockMapper.ToCommand(request: request, gameId: gameId, holderName: this.ActorId), cancellationToken: cancellationToken);
                        return this.Ok(value: EditLockMapper.ToResponse(dto: editLock));
                }

                /// <summary>
                /// Extends the edit lock by 15 minutes.
                /// While the editor is open, the frontend calls this endpoint every few minutes,
                /// so the lock does not run out while the employee is still working.
                /// </summary>
                /// <remarks>
                /// 204 when the lock is renewed. 404 when the game does not exist.
                /// 409 when the lock token is wrong or the lock has already expired.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the lock is renewed.</returns>
                [HttpPut(template: "")]
                [EndpointSummary(summary: "Forlænger låsen på et spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult> RenewLockAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, CancellationToken cancellationToken)
                {
                        await this._renewLock.HandleAsync(command: new RenewLockCommand(GameId: gameId, LockToken: lockToken), cancellationToken: cancellationToken);
                        return this.NoContent();
                }

                /// <summary>
                /// Gives the edit lock free.
                /// When the employee closes the editor, the frontend calls this endpoint,
                /// so a colleague can edit the game right away instead of waiting 15 minutes.
                /// Calling it with a wrong or old token does nothing, so it is always safe to call.
                /// </summary>
                /// <remarks>
                /// 204 when the lock is released (or was not held). 404 when the game does not exist.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content.</returns>
                [HttpDelete(template: "")]
                [EndpointSummary(summary: "Frigiver låsen på et spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                public async Task<ActionResult> ReleaseLockAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, CancellationToken cancellationToken)
                {
                        await this._releaseLock.HandleAsync(command: new ReleaseLockCommand(GameId: gameId, LockToken: lockToken), cancellationToken: cancellationToken);
                        return this.NoContent();
                }
        }
}
