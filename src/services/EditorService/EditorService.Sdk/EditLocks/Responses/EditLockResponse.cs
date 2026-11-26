using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.EditLocks.Responses
{
        /// <summary>
        /// The edit lock sent back to the editor.
        /// The editor sends the token in the X-Lock-Token header on every change,
        /// and renews the lock before it expires.
        /// </summary>
        /// <param name="LockToken">The secret token that proves the employee holds the lock.</param>
        /// <param name="ExpiresAt">When the lock runs out, unless it is renewed.</param>
        public sealed record EditLockResponse(Guid LockToken, DateTimeOffset ExpiresAt);
}
