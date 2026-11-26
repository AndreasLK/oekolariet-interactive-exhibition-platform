using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.DTOs.EditLocks
{
        /// <summary>
        /// The edit lock the employee got. The token is sent in the X-Lock-Token header on every change.
        /// </summary>
        public sealed record EditLockDto
        {
                /// <summary>The secret token that proves the employee holds the lock.</summary>
                public required Guid LockToken { get; init; }

                /// <summary>When the lock runs out, unless it is renewed.</summary>
                public required DateTimeOffset ExpiresAt { get; init; }
        }
}
