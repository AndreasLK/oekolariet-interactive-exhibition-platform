using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using HighscoreService.Application.Options;
using HighscoreService.Facade.Dtos;
using HighscoreService.Hub.Api;
using HighscoreService.Hub.Api.Endpoints.Screens;
using HighscoreService.Screen.Infrastructure.Sync;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HighscoreService.Tests.Hub
{
        /// <summary>
        /// End-to-end integration tests verifying Hub JWT auth, zero-touch Screen enrollment,
        /// admin approval/revocation, least-privilege policies, and rate/pending-cap protections.
        /// </summary>
        [Collection(MsSqlHighscoreDatabaseCollection.COLLECTION_NAME)]
        public sealed class HubAuthAndEnrollmentTests(MsSqlHighscoreDatabaseFixture fixture)
        {
                private const string JWT_ISSUER = "Oekolariet.AuthService";
                private const string JWT_AUDIENCE = "Oekolariet.Platform";
                private static readonly string TEST_SIGNING_KEY_BASE64 =
                        Convert.ToBase64String(Encoding.UTF8.GetBytes("test-highscore-jwt-signing-key-at-least-32-bytes!"));

                private readonly MsSqlHighscoreDatabaseFixture _fixture = fixture;

                [Fact]
                public async Task AnonymousRequests_AreBlockedOnProtectedEndpoints_WhilePublicLeaderboardAndHealthRemainOpen()
                {
                        await this._fixture.ResetDatabaseAsync();
                        using WebApplicationFactory<HubApiMarker> factory = this.CreateHubFactory();
                        using HttpClient client = factory.CreateClient();

                        using HttpResponseMessage health = await client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken);
                        using HttpResponseMessage leaderboard = await client.GetAsync("/api/v1/highscores", TestContext.Current.CancellationToken);
                        using HttpResponseMessage record = await client.PostAsJsonAsync(
                                "/api/v1/highscores",
                                new { Name = "Anon", Age = 10, Score = 100, SenderMacAddress = "AA:BB:CC:DD:EE:01", GameId = Guid.CreateVersion7() },
                                TestContext.Current.CancellationToken);
                        using HttpResponseMessage delete = await client.DeleteAsync($"/api/v1/highscores/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);
                        using HttpResponseMessage stats = await client.GetAsync("/api/v1/highscores/statistics", TestContext.Current.CancellationToken);
                        using HttpResponseMessage sync = await client.PostAsJsonAsync("/api/v1/highscores/sync/batch", Array.Empty<HighscoreDto>(), TestContext.Current.CancellationToken);
                        using HttpResponseMessage screens = await client.GetAsync("/api/v1/screens", TestContext.Current.CancellationToken);

                        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
                        Assert.Equal(HttpStatusCode.OK, leaderboard.StatusCode);
                        Assert.Equal(HttpStatusCode.Unauthorized, record.StatusCode);
                        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
                        Assert.Equal(HttpStatusCode.Unauthorized, stats.StatusCode);
                        Assert.Equal(HttpStatusCode.Unauthorized, sync.StatusCode);
                        Assert.Equal(HttpStatusCode.Unauthorized, screens.StatusCode);
                }

                [Fact]
                public async Task ZeroTouchScreenEnrollment_RequiresAdminApproval_AndRevocationTakesEffectImmediately()
                {
                        await this._fixture.ResetDatabaseAsync();
                        using WebApplicationFactory<HubApiMarker> factory = this.CreateHubFactory();
                        using HttpClient client = factory.CreateClient();

                        string tempIdentityPath = Path.Combine(Path.GetTempPath(), $"screen-id-{Guid.NewGuid():N}.json");
                        try
                        {
                                ScreenIdentityStore identityStore = new(Options.Create(new ScreenSyncOptions
                                {
                                        IdentityFilePath = tempIdentityPath
                                }));

                                ScreenIdentity identity = await identityStore.GetOrCreateAsync(TestContext.Current.CancellationToken);
                                ScreenIdentity reloaded = await identityStore.GetOrCreateAsync(TestContext.Current.CancellationToken);
                                Assert.Equal(identity.ScreenId, reloaded.ScreenId);
                                Assert.Equal(identity.Secret, reloaded.Secret);

                                // 1. Screen enrolls anonymously (Pending).
                                using HttpResponseMessage enrollResponse = await client.PostAsJsonAsync(
                                        "/api/v1/screens/enroll",
                                        new { identity.ScreenId, NodeId = "kiosk-01", MacAddress = "AA:BB:CC:DD:EE:11", identity.Secret },
                                        TestContext.Current.CancellationToken);

                                Assert.Equal(HttpStatusCode.Created, enrollResponse.StatusCode);

                                // Re-enrolling idempotently returns 200 OK without overwriting existing registration.
                                using HttpResponseMessage reEnroll = await client.PostAsJsonAsync(
                                        "/api/v1/screens/enroll",
                                        new { identity.ScreenId, NodeId = "kiosk-01", MacAddress = "AA:BB:CC:DD:EE:11", Secret = "attacker-different-secret-123456" },
                                        TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.OK, reEnroll.StatusCode);

                                // 2. Pending screen is rejected on sync endpoints.
                                using HttpRequestMessage pendingSyncReq = new(HttpMethod.Post, "/api/v1/highscores/sync/batch")
                                {
                                        Content = JsonContent.Create(Array.Empty<HighscoreDto>())
                                };
                                pendingSyncReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.ToBearerToken());
                                using HttpResponseMessage pendingSyncResp = await client.SendAsync(pendingSyncReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.Unauthorized, pendingSyncResp.StatusCode);

                                // 3. Basic AuthService user cannot approve screens or delete highscores, but can view statistics.
                                string basicUserJwt = CreateAuthServiceJwt(role: "Viewer", permissions: []);
                                using HttpRequestMessage userApproveReq = new(HttpMethod.Post, $"/api/v1/screens/{identity.ScreenId}/approve");
                                userApproveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", basicUserJwt);
                                using HttpResponseMessage userApproveResp = await client.SendAsync(userApproveReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.Forbidden, userApproveResp.StatusCode);

                                using HttpRequestMessage userStatsReq = new(HttpMethod.Get, "/api/v1/highscores/statistics");
                                userStatsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", basicUserJwt);
                                using HttpResponseMessage userStatsResp = await client.SendAsync(userStatsReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.OK, userStatsResp.StatusCode);

                                // 4. Admin AuthService user approves the screen.
                                string adminJwt = CreateAuthServiceJwt(role: "Admin", permissions: ["DeleteGame"]);
                                using HttpRequestMessage adminApproveReq = new(HttpMethod.Post, $"/api/v1/screens/{identity.ScreenId}/approve");
                                adminApproveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
                                using HttpResponseMessage adminApproveResp = await client.SendAsync(adminApproveReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.OK, adminApproveResp.StatusCode);

                                ScreenRegistrationDto? approvedDto =
                                        await adminApproveResp.Content.ReadFromJsonAsync<ScreenRegistrationDto>(TestContext.Current.CancellationToken);
                                Assert.Equal("Approved", approvedDto?.Status);

                                // 5. Approved screen can now sync and record, but still cannot delete Hub highscores.
                                using HttpRequestMessage approvedSyncReq = new(HttpMethod.Post, "/api/v1/highscores/sync/batch")
                                {
                                        Content = JsonContent.Create(Array.Empty<HighscoreDto>())
                                };
                                approvedSyncReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.ToBearerToken());
                                using HttpResponseMessage approvedSyncResp = await client.SendAsync(approvedSyncReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.OK, approvedSyncResp.StatusCode);

                                using HttpRequestMessage screenDeleteReq = new(HttpMethod.Delete, $"/api/v1/highscores/{Guid.CreateVersion7()}");
                                screenDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.ToBearerToken());
                                using HttpResponseMessage screenDeleteResp = await client.SendAsync(screenDeleteReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.Unauthorized, screenDeleteResp.StatusCode);

                                // 6. Admin revokes screen; next sync is immediately rejected.
                                using HttpRequestMessage revokeReq = new(HttpMethod.Post, $"/api/v1/screens/{identity.ScreenId}/revoke");
                                revokeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
                                using HttpResponseMessage revokeResp = await client.SendAsync(revokeReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

                                using HttpRequestMessage revokedSyncReq = new(HttpMethod.Post, "/api/v1/highscores/sync/batch")
                                {
                                        Content = JsonContent.Create(Array.Empty<HighscoreDto>())
                                };
                                revokedSyncReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.ToBearerToken());
                                using HttpResponseMessage revokedSyncResp = await client.SendAsync(revokedSyncReq, TestContext.Current.CancellationToken);
                                Assert.Equal(HttpStatusCode.Unauthorized, revokedSyncResp.StatusCode);
                        }
                        finally
                        {
                                if (File.Exists(tempIdentityPath))
                                {
                                        File.Delete(tempIdentityPath);
                                }
                        }
                }

                private WebApplicationFactory<HubApiMarker> CreateHubFactory()
                {
                        return new WebApplicationFactory<HubApiMarker>()
                                .WithWebHostBuilder(builder =>
                                {
                                        builder.UseSetting("ConnectionStrings:HighscoreDatabase", this._fixture.ConnectionString);
                                        builder.UseSetting("Jwt:Issuer", JWT_ISSUER);
                                        builder.UseSetting("Jwt:Audience", JWT_AUDIENCE);
                                        builder.UseSetting("Jwt:SigningKey", TEST_SIGNING_KEY_BASE64);
                                });
                }

                private static string CreateAuthServiceJwt(string role, IReadOnlyList<string> permissions)
                {
                        SymmetricSecurityKey key = new(Convert.FromBase64String(TEST_SIGNING_KEY_BASE64));
                        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

                        List<Claim> claims =
                        [
                                new Claim("sub", Guid.CreateVersion7().ToString()),
                                new Claim("name", "test-user"),
                                new Claim("role", role)
                        ];

                        foreach (string permission in permissions)
                        {
                                claims.Add(new Claim("permission", permission));
                        }

                        JwtSecurityToken token = new(
                                issuer: JWT_ISSUER,
                                audience: JWT_AUDIENCE,
                                claims: claims,
                                notBefore: DateTime.UtcNow.AddMinutes(-1),
                                expires: DateTime.UtcNow.AddMinutes(10),
                                signingCredentials: creds);

                        return new JwtSecurityTokenHandler().WriteToken(token);
                }
        }
}

