using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Exceptions
{
        /// <summary>
        /// Thrown when two editors save the same game at the same time.
        /// </summary>
        public sealed class ConcurrencyConflictException : DomainException
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class.
                /// </summary>
                /// <param name="message">The message that describes the conflict.</param>
                public ConcurrencyConflictException(string message) : base(message: message)
                {
                }

                /// <summary>
                /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class with the original error.
                /// </summary>
                /// <param name="message">The message that describes the conflict.</param>
                /// <param name="innerException">The original error from the database.</param>
                public ConcurrencyConflictException(string message, Exception innerException) : base(message: message, innerException: innerException)
                {
                }
        }
}
