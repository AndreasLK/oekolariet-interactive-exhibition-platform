
namespace AuthService.Domain.ValueObjects;

public sealed record AuthSessionId
{
        public Guid Value { get; }

        private AuthSessionId(Guid value)
        {
                this.Value = value;
        }

        public static AuthSessionId CreateNew()
        {
                return new AuthSessionId(value: Guid.NewGuid());
        }

        public static AuthSessionId From(Guid value)
        {
                if (value == Guid.Empty)
                {
                        throw new ArgumentException(
                                message: "Session ID cannot be empty.",
                                paramName: nameof(value));
                }

                return new AuthSessionId(value: value);
        }
}
