using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using AuthService.Application.Authentication;
using AuthService.Infrastructure.Authentication;
using AuthService.Application.Sessions;
using AuthService.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);


const string AUTH_CONNECTION_NAME = "AuthDatabase";
const string MISSING_CONNECTION_STRING =
        "The AuthDatabase connection string is missing.";

string connectionString = builder.Configuration
        .GetConnectionString(name: AUTH_CONNECTION_NAME)
        ?? throw new InvalidOperationException(
                message: MISSING_CONNECTION_STRING);

// DI: EF Core creates a scoped DbContext for each HTTP request.
builder.Services.AddDbContext<AuthDbContext>(
        optionsAction: options => options.UseSqlite(
                connectionString: connectionString));


// DIP: Application depends on the repository contract, not EF Core.
// Scoped lifetime shares the request's DbContext for consistent persistence.
builder.Services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();




// Identity handles credential storage and password hashing.
// JWT authentication will be configured separately.
builder.Services.AddIdentityCore<AuthIdentityUser>()
        .AddEntityFrameworkStores<AuthDbContext>();
builder.Services.AddScoped<InitialAdminBootstrapper>();

// Add services to the container.

const string JWT_SECTION = "Jwt";
const string JWT_ISSUER = "Issuer";
const string JWT_AUDIENCE = "Audience";
const string JWT_SIGNING_KEY = "SigningKey";

const string INVALID_JWT_CONFIGURATION =
        "JWT configuration is missing or invalid.";

const string INVALID_SIGNING_KEY =
        "JWT signing key must contain at least 32 bytes.";

const int MIN_SIGNING_KEY_BYTES = 32;

IConfigurationSection jwtConfiguration =
        builder.Configuration.GetRequiredSection(JWT_SECTION);

string? issuer = jwtConfiguration[JWT_ISSUER];
string? audience = jwtConfiguration[JWT_AUDIENCE];
string? signingKey = jwtConfiguration[JWT_SIGNING_KEY];

if (string.IsNullOrWhiteSpace(issuer)
        || string.IsNullOrWhiteSpace(audience)
        || string.IsNullOrWhiteSpace(signingKey))
{
        throw new InvalidOperationException(
                message: INVALID_JWT_CONFIGURATION);
}

// Decode the cryptographically generated key stored in User Secrets.
byte[] signingKeyBytes = Convert.FromBase64String(signingKey);

if (signingKeyBytes.Length < MIN_SIGNING_KEY_BYTES)
{
        throw new InvalidOperationException(
                message: INVALID_SIGNING_KEY);
}

// JWT Bearer validates incoming tokens before authorization policies run.
builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
                options.MapInboundClaims = false;

                options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey =
                                        new SymmetricSecurityKey(signingKeyBytes),

                                ValidateIssuer = true,
                                ValidIssuer = issuer,

                                ValidateAudience = true,
                                ValidAudience = audience,

                                ValidateLifetime = true,
                                RequireExpirationTime = true,
                                RequireSignedTokens = true,
                                ClockSkew = TimeSpan.Zero
                        };
        });

builder.Services.AddAuthorization();


const string JWT_ACCESS_TOKEN_MINUTES = "AccessTokenMinutes";
const string INVALID_TOKEN_LIFETIME =
        "JWT access token lifetime must be positive.";

int accessTokenMinutes = jwtConfiguration.GetValue<int>(
        key: JWT_ACCESS_TOKEN_MINUTES);

if (accessTokenMinutes <= 0)
{
        throw new InvalidOperationException(
                message: INVALID_TOKEN_LIFETIME);
}

// TimeProvider makes token expiration deterministic in unit tests.
builder.Services.AddSingleton<TimeProvider>(
        implementationInstance: TimeProvider.System);

// DIP: The API resolves the interface, not the concrete JWT implementation.
builder.Services.AddSingleton<IAccessTokenIssuer>(
        implementationFactory: serviceProvider =>
                new JwtAccessTokenIssuer(
                        issuer: issuer,
                        audience: audience,
                        signingKeyBytes: signingKeyBytes,
                        accessTokenLifetime: TimeSpan.FromMinutes(
                                minutes: accessTokenMinutes),
                        timeProvider: serviceProvider.GetRequiredService<TimeProvider>()));


// DIP: Infrastructure provides secure refresh-token generation.
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();



builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();


const string BOOTSTRAP_COMMAND = "--bootstrap-admin";
const string ADMIN_USERNAME_KEY = "BootstrapAdmin:Username";
const string ADMIN_PASSWORD_KEY = "BootstrapAdmin:Password";

const string DEVELOPMENT_ONLY =
        "Administrator bootstrap is only allowed in Development.";

const string MISSING_CREDENTIALS =
        "Administrator bootstrap credentials are missing.";

if (args.Length == 1 && args[0] == BOOTSTRAP_COMMAND)
{
        // Bootstrap is explicitly invoked, never part of normal API startup.
        if (!app.Environment.IsDevelopment())
        {
                throw new InvalidOperationException(
                        message: DEVELOPMENT_ONLY);
        }

        string? username = app.Configuration[ADMIN_USERNAME_KEY];
        string? password = app.Configuration[ADMIN_PASSWORD_KEY];

        // Fail early instead of creating an account with invalid credentials.
        if (string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(password))
        {
                throw new InvalidOperationException(
                        message: MISSING_CREDENTIALS);
        }

        // A DI scope keeps UserManager and DbContext scoped together.
        using IServiceScope scope = app.Services.CreateScope();

        InitialAdminBootstrapper bootstrapper =
                scope.ServiceProvider
                        .GetRequiredService<InitialAdminBootstrapper>();

        await bootstrapper.RunAsync(
                username: username,
                password: password);

        Console.WriteLine("Initial administrator created successfully.");

        // Exit before the HTTP server starts.
        return;
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
        app.MapOpenApi();
}

app.UseHttpsRedirection();

// Authentication identifies the caller before authorization checks permissions.
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
