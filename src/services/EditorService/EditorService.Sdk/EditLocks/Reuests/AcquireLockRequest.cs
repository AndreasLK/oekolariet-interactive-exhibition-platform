using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.EditLocks.Reuests
{
        /// <summary>
        /// The body sent by the editor when it opens a game and wants the edit lock.
        /// The employee's name is not sent. It comes from the login in the Api.
        /// </summary>
        /// <param name="DeviceName">The device the employee is using, e.g. "Kontor-PC 2".</param>
        public sealed record AcquireLockRequest(string DeviceName);
}
