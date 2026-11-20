using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Exceptions
{
        /// <summary>
        /// Thrown when a domain rule or input validation is violated.
        /// </summary>
        public sealed class DomainValidationException : DomainException
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="DomainValidationException"/> class.
                /// </summary>
                /// <param name="message">The message that describes the validation error.</param>
                public DomainValidationException(string message) : base(message: message)
                {
                }
        }
}
