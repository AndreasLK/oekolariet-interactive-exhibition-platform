using HighscoreService.Domain.Entities;

namespace HighscoreService.Hub.Api.Endpoints.Screens
{
        /// <summary>
        /// Summary DTO returned for enrolled screens (never exposes <see cref="ScreenRegistration.SecretHash"/>).
        /// </summary>
        /// <param name="ScreenId">Screen identifier.</param>
        /// <param name="NodeId">Node name.</param>
        /// <param name="MacAddress">Optional MAC address.</param>
        /// <param name="Status">Approval status.</param>
        /// <param name="CreatedAtUtc">Enrollment timestamp.</param>
        /// <param name="ApprovedAtUtc">Approval timestamp, if approved.</param>
        /// <param name="RevokedAtUtc">Revocation timestamp, if revoked.</param>
        public sealed record ScreenRegistrationDto(
                Guid ScreenId,
                string NodeId,
                string? MacAddress,
                string Status,
                DateTimeOffset CreatedAtUtc,
                DateTimeOffset? ApprovedAtUtc,
                DateTimeOffset? RevokedAtUtc);
}

