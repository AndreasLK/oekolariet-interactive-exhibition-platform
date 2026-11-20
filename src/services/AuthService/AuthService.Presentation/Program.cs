using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

// Identity handles credential storage and password hashing.
// JWT authentication will be configured separately.
builder.Services.AddIdentityCore<AuthIdentityUser>()
        .AddEntityFrameworkStores<AuthDbContext>();


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
        app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
