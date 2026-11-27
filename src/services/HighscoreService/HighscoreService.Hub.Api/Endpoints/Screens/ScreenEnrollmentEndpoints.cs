using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;
using HighscoreService.Hub.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.Endpoints.Screens
{
        /// <summary>
        /// Endpoints for zero-touch exhibition screen enrollment and administrator approval/revocation.
        /// </summary>
        public static class ScreenEnrollmentEndpoints
        {
                private const int MAX_PENDING_ENROLLMENTS = 50;

                /// <summary>
                /// Maps screen enrollment (rate-limited anonymous) and admin management endpoints.
                /// </summary>
                /// <param name="app">Endpoint route builder.</param>
                /// <returns>The updated route builder.</returns>
                public static IEndpointRouteBuilder MapScreenEnrollmentEndpoints(this IEndpointRouteBuilder app)
                {
                        // Anonymous enrollment is rate-limited so unauthenticated kiosks can register on first boot.
                        app.MapPost("/api/v1/screens/enroll", HandleEnrollAsync)
                                .AllowAnonymous()
                                .RequireRateLimiting(HubAuthSetup.ENROLL_RATE_LIMIT_POLICY)
                                .WithName("EnrollScreen")
                                .Produces<ScreenRegistrationDto>(StatusCodes.Status200OK)
                                .Produces<ScreenRegistrationDto>(StatusCodes.Status201Created)
                                .Produces(StatusCodes.Status429TooManyRequests);

                        // Listing, approving, and revoking screens require Hub administrator authorization.
                        RouteGroupBuilder adminGroup = app.MapGroup("/api/v1/screens")
                                .RequireAuthorization(HubAuthSetup.POLICY_HUB_ADMIN);

                        adminGroup.MapGet("/", HandleListAsync)
                                .WithName("ListScreens")
                                .Produces<IReadOnlyList<ScreenRegistrationDto>>(StatusCodes.Status200OK);

                        adminGroup.MapPost("/{screenId:guid}/approve", HandleApproveAsync)
                                .WithName("ApproveScreen")
                                .Produces<ScreenRegistrationDto>(StatusCodes.Status200OK)
                                .Produces(StatusCodes.Status404NotFound);

                        adminGroup.MapPost("/{screenId:guid}/revoke", HandleRevokeAsync)
                                .WithName("RevokeScreen")
                                .Produces<ScreenRegistrationDto>(StatusCodes.Status200OK)
                                .Produces(StatusCodes.Status404NotFound);

                        return app;
                }

                private static async Task<IResult> HandleEnrollAsync(
                        [FromBody] EnrollScreenRequest request,
                        [FromServices] IScreenRegistrationStore store,
                        TimeProvider timeProvider,
                        CancellationToken cancellationToken)
                {
                        // Construct the domain registration in Pending state with a hashed secret.
                        ScreenRegistration registration = ScreenRegistration.Enroll(
                                screenId: request.ScreenId,
                                nodeId: request.NodeId,
                                macAddress: request.MacAddress,
                                rawSecret: request.Secret,
                                createdAtUtc: timeProvider.GetUtcNow());

                        // Persist idempotently while enforcing the pending-enrollment queue cap.
                        ScreenEnrollmentOutcome outcome = await store.EnrollIdempotentAsync(
                                registration: registration,
                                maxPendingCount: MAX_PENDING_ENROLLMENTS,
                                cancellationToken: cancellationToken);

                        if (outcome.PendingCapReached || outcome.Registration is null)
                        {
                                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                        }

                        // Return 201 on first enrollment and 200 on idempotent retries without leaking the secret hash.
                        ScreenRegistrationDto dto = ToDto(outcome.Registration);
                        return outcome.WasCreated
                                ? Results.Created($"/api/v1/screens/{dto.ScreenId}", dto)
                                : Results.Ok(dto);
                }

                private static async Task<IResult> HandleListAsync(
                        [FromServices] IScreenRegistrationStore store,
                        CancellationToken cancellationToken)
                {
                        // Fetch all registrations and strip secret hashes before returning.
                        IReadOnlyList<ScreenRegistration> screens = await store.ListAsync(cancellationToken);
                        return Results.Ok(screens.Select(ToDto).ToList());
                }

                private static async Task<IResult> HandleApproveAsync(
                        Guid screenId,
                        [FromServices] IScreenRegistrationStore store,
                        TimeProvider timeProvider,
                        CancellationToken cancellationToken)
                {
                        // Transition the pending screen to Approved using the current UTC timestamp.
                        ScreenRegistration? updated = await store.ApproveAsync(
                                screenId: screenId,
                                approvedAtUtc: timeProvider.GetUtcNow(),
                                cancellationToken: cancellationToken);

                        return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
                }

                private static async Task<IResult> HandleRevokeAsync(
                        Guid screenId,
                        [FromServices] IScreenRegistrationStore store,
                        TimeProvider timeProvider,
                        CancellationToken cancellationToken)
                {
                        // Transition the screen to Revoked so subsequent bearer auth checks fail immediately.
                        ScreenRegistration? updated = await store.RevokeAsync(
                                screenId: screenId,
                                revokedAtUtc: timeProvider.GetUtcNow(),
                                cancellationToken: cancellationToken);

                        return updated is null ? Results.NotFound() : Results.Ok(ToDto(updated));
                }

                private static ScreenRegistrationDto ToDto(ScreenRegistration entity) =>
                        new(
                                ScreenId: entity.ScreenId,
                                NodeId: entity.NodeId,
                                MacAddress: entity.MacAddress,
                                Status: entity.Status.ToString(),
                                CreatedAtUtc: entity.CreatedAtUtc,
                                ApprovedAtUtc: entity.ApprovedAtUtc,
                                RevokedAtUtc: entity.RevokedAtUtc);
        }
}

