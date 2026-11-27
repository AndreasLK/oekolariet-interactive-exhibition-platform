
using System.Security.Cryptography;
using AuthService.Application.Authentication;
using AuthService.Domain.Authorization;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AuthService.Infrastructure.Tests.Authentication;

public sealed class JwtAccessTokenIssuerTests
{
        private const string INVALID_AUDIENCE = "Invalid.Audience";
        private const string TEST_ISSUER = "Oekolariet.AuthService";
        private const string TEST_AUDIENCE = "Oekolariet.Platform";
        private const string TEST_USERNAME = "LimitedStaffTest";
        private const string PERMISSION_CLAIM_TYPE = "permission";

        private const int SIGNING_KEY_BYTES = 64;
        private const int ACCESS_TOKEN_MINUTES = 15;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task IssuedTokenContainsEffectivePermissionsAsync(
                bool grantPublishPermission)
        {
                byte[] signingKey = RandomNumberGenerator.GetBytes(
                        count: SIGNING_KEY_BYTES);

                UserAccount account = new(
                        id: UserId.CreateNew(),
                        username: Username.Create(value: TEST_USERNAME),
                        role: RoleProfile.LimitedStaff);

                if (grantPublishPermission)
                {
                        account.SetPermissionOverride(
                                permission: Permission.PublishGame,
                                isAllowed: true);
                }

                JwtAccessTokenIssuer issuer = new(
                        issuer: TEST_ISSUER,
                        audience: TEST_AUDIENCE,
                        signingKeyBytes: signingKey,
                        accessTokenLifetime: TimeSpan.FromMinutes(
                                minutes: ACCESS_TOKEN_MINUTES),
                        timeProvider: TimeProvider.System);

                IssuedAccessToken token = issuer.Issue(account: account);

                JsonWebTokenHandler handler = new();

                // Validate the signature and token metadata before trusting claims.
                TokenValidationParameters validationParameters = new()
                {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                                key: signingKey),
                        ValidateIssuer = true,
                        ValidIssuer = TEST_ISSUER,
                        ValidateAudience = true,
                        ValidAudience = TEST_AUDIENCE,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,
                        ClockSkew = TimeSpan.Zero
                };

                TokenValidationResult validation =
                        await handler.ValidateTokenAsync(
                                token: token.Value,
                                validationParameters: validationParameters);

                Assert.True(condition: validation.IsValid);
                Assert.NotNull(validation.ClaimsIdentity);

                string[] permissions = validation.ClaimsIdentity
                        .FindAll(PERMISSION_CLAIM_TYPE)
                        .Select(claim => claim.Value)
                        .ToArray();

                Assert.Contains(
                        expected: nameof(Permission.CreateGame),
                        collection: permissions);

                // Permission overrides must affect the issued JWT claims.
                if (grantPublishPermission)
                {
                        Assert.Contains(
                                expected: nameof(Permission.PublishGame),
                                collection: permissions);

                        return;
                }

                Assert.DoesNotContain(
                        expected: nameof(Permission.PublishGame),
                        collection: permissions);
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TokenValidationRejectsInvalidSignatureOrAudienceAsync(
                bool useWrongSigningKey)
        {
                byte[] signingKey = RandomNumberGenerator.GetBytes(
                        count: SIGNING_KEY_BYTES);

                UserAccount account = new(
                        id: UserId.CreateNew(),
                        username: Username.Create(value: TEST_USERNAME),
                        role: RoleProfile.Staff);

                JwtAccessTokenIssuer issuer = new(
                        issuer: TEST_ISSUER,
                        audience: TEST_AUDIENCE,
                        signingKeyBytes: signingKey,
                        accessTokenLifetime: TimeSpan.FromMinutes(
                                minutes: ACCESS_TOKEN_MINUTES),
                        timeProvider: TimeProvider.System);

                IssuedAccessToken token = issuer.Issue(account: account);

                byte[] validationKey = useWrongSigningKey
                        ? RandomNumberGenerator.GetBytes(count: SIGNING_KEY_BYTES)
                        : signingKey;

                string validationAudience = useWrongSigningKey
                        ? TEST_AUDIENCE
                        : INVALID_AUDIENCE;

                TokenValidationParameters parameters = new()
                {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                                key: validationKey),
                        ValidateIssuer = true,
                        ValidIssuer = TEST_ISSUER,
                        ValidateAudience = true,
                        ValidAudience = validationAudience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,
                        ClockSkew = TimeSpan.Zero
                };

                JsonWebTokenHandler handler = new();

                // A valid signature alone does not guarantee a trusted token.
                TokenValidationResult result = await handler.ValidateTokenAsync(
                        token: token.Value,
                        validationParameters: parameters);

                Assert.False(condition: result.IsValid);
        }

}
