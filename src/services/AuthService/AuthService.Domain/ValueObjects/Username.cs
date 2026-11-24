namespace AuthService.Domain.ValueObjects;

public sealed record Username
{
        private const string USERNAME_REQUIRED = "Username is required.";

        public string Value { get; }

        private Username(string value)
        {
                this.Value = value;
        }

        public static Username Create(string value)
        {
                if (string.IsNullOrWhiteSpace(value))
                {
                        throw new ArgumentException(
                                message: USERNAME_REQUIRED,
                                paramName: nameof(value));
                }

                return new Username(value: value.Trim());
        }
}
