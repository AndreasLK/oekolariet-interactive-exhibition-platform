using Microsoft.AspNetCore.Mvc;

namespace EditorService.Api.Controllers
{
        /// <summary>
        /// Base class for all Editor controllers. Holds what every controller needs.
        /// </summary>
        public abstract class EditorControllerBase : ControllerBase
        {
                /// <summary>
                /// Gets the employee making the request. Used for CreatedBy and UpdatedBy.
                /// </summary>
                // TODO: read the employee from the JWT when login is added.
                protected string ActorId => this.User.Identity?.Name ?? "unknown";
        }
}
