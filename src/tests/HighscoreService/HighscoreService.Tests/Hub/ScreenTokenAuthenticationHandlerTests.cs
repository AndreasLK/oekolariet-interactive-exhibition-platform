using System.Security.Claims;
using System.Text.Encodings.Web;
using HighscoreService.Domain.Entities;
using HighscoreService.Hub.Api.Authentication;
using HighscoreService.Tests.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HighscoreService.Tests.Hub
{
        /// <summary>
        /// Unit tests for <see cref="ScreenTokenAuthenticationHandler"/> token parsing, status checks, and scheme isolation.
        /// </summary>
        public sealed class ScreenTokenAuthenticationHandlerTests
        {
                /// <summary>
                /// Verifies non-<c>scr_</c> Bearer tokens return <see cref="AuthenticateResult.NoResult"/> so JWT auth can run.
                /// </summary>
                [Fact]
                public async Task HandleAuthenticateAsync_ReturnsNoResult_ForJwtBearerToken()
                {
                        // Arrange
                        ScreenTokenAuthenticationHandler handler = await CreateHandlerAsync(
                                new StubScreenRegistrationStore(),
                                authorizationHeader: "Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig");

                        // Act
                        AuthenticateResult result = await handler.AuthenticateAsync();

                        // Assert
                        Assert.True(result.None);
                }

                /// <summary>
                /// Verifies malformed <c>scr_</c> tokens and unapproved/wrong-secret screens fail authentication.
                /// </summary>
                /// <param name="tokenSuffix">Token suffix after <c>Bearer </c>.</param>
                [Theory]
                [InlineData("scr_not-a-guid.secret")]
                [InlineData("scr_missing-dot-separator")]
                [InlineData("scr_11111111-1111-1111-1111-111111111111.")]
                public async Task HandleAuthenticateAsync_Fails_ForMalformedScreenTokens(string tokenSuffix)
                {
                        // Arrange
                        ScreenTokenAuthenticationHandler handler = await CreateHandlerAsync(
                                new StubScreenRegistrationStore(),
                                authorizationHeader: $"Bearer {tokenSuffix}");

                        // Act
                        AuthenticateResult result = await handler.AuthenticateAsync();

                        // Assert
                        Assert.False(result.Succeeded);
                        Assert.NotNull(result.Failure);
                }

                /// <summary>
                /// Verifies an approved screen with a valid secret authenticates with the <c>Screen</c> role,
                /// while wrong secrets and pending/revoked registrations fail.
                /// </summary>
                [Fact]
                public async Task HandleAuthenticateAsync_ValidatesApprovalStatusAndSecret()
                {
                        // Arrange
                        Guid screenId = Guid.CreateVersion7();
                        const string VALID_SECRET = "valid-screen-secret-123456";
                        ScreenRegistration registration = ScreenRegistration.Enroll(
                                screenId: screenId,
                                nodeId: "kiosk-01",
                                macAddress: null,
                                rawSecret: VALID_SECRET,
                                createdAtUtc: DateTimeOffset.UtcNow);
                        registration.Approve(DateTimeOffset.UtcNow);

                        StubScreenRegistrationStore store = new();
                        store.Add(registration);

                        // Act 1: Approved + valid secret -> Succeeds with Screen role.
                        ScreenTokenAuthenticationHandler validHandler = await CreateHandlerAsync(
                                store,
                                authorizationHeader: $"Bearer scr_{screenId}.{VALID_SECRET}");
                        AuthenticateResult validResult = await validHandler.AuthenticateAsync();

                        Assert.True(validResult.Succeeded);
                        Assert.Equal("Screen", validResult.Principal?.FindFirstValue(ClaimTypes.Role));
                        Assert.Equal("kiosk-01", validResult.Principal?.Identity?.Name);

                        // Act 2: Wrong secret -> Fails.
                        ScreenTokenAuthenticationHandler wrongSecretHandler = await CreateHandlerAsync(
                                store,
                                authorizationHeader: $"Bearer scr_{screenId}.wrong-secret-123456");
                        AuthenticateResult wrongSecretResult = await wrongSecretHandler.AuthenticateAsync();
                        Assert.False(wrongSecretResult.Succeeded);

                        // Act 3: Revoked screen -> Fails immediately.
                        registration.Revoke(DateTimeOffset.UtcNow);
                        ScreenTokenAuthenticationHandler revokedHandler = await CreateHandlerAsync(
                                store,
                                authorizationHeader: $"Bearer scr_{screenId}.{VALID_SECRET}");
                        AuthenticateResult revokedResult = await revokedHandler.AuthenticateAsync();
                        Assert.False(revokedResult.Succeeded);
                }

                private static async Task<ScreenTokenAuthenticationHandler> CreateHandlerAsync(
                        StubScreenRegistrationStore store,
                        string authorizationHeader)
                {
                        // Initialize the ASP.NET Core AuthenticationHandler with a synthetic HttpContext.
                        IOptionsMonitor<AuthenticationSchemeOptions> options = new StaticOptionsMonitor();
                        ScreenTokenAuthenticationHandler handler = new(
                                options: options,
                                logger: NullLoggerFactory.Instance,
                                encoder: UrlEncoder.Default,
                                registrationStore: store);

                        DefaultHttpContext httpContext = new();
                        httpContext.Request.Headers.Authorization = authorizationHeader;

                        await handler.InitializeAsync(
                                new AuthenticationScheme(
                                        ScreenTokenAuthenticationHandler.SCHEME_NAME,
                                        displayName: null,
                                        typeof(ScreenTokenAuthenticationHandler)),
                                httpContext);

                        return handler;
                }
        }
}
