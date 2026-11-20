using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Exceptions
{
        public abstract class DomainException : Exception
        {
                // Constructor 1: only a message
                protected DomainException(string message) : base(message: message)
                {
                }

                // Constructor 2: a message and the original error
                protected DomainException(string message, Exception innerException) : base(message: message, innerException: innerException)
                {
                }
        }
}
