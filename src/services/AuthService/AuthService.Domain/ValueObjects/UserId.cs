namespace AuthService.Domain.ValueObjects;

public sealed record UserId
{
        public Guid Value { get; }

        private UserId(Guid value)
        {
                this.Value = value;
        }

        public static UserId CreateNew()
        {
                return new UserId(value: Guid.NewGuid());
        }

        public static UserId From(Guid value)
        {
                if (value == Guid.Empty)
                {
                        throw new ArgumentException(
                                message: "User ID cannot be empty.",
                                paramName: nameof(value));
                }

                return new UserId(value: value);
        }
}
