# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Copy all project files first so NuGet restore is cached across source edits.
COPY HighscoreService.Domain/HighscoreService.Domain.csproj HighscoreService.Domain/
COPY HighscoreService.Facade/HighscoreService.Facade.csproj HighscoreService.Facade/
COPY HighscoreService.Application/HighscoreService.Application.csproj HighscoreService.Application/
COPY HighscoreService.Sdk/HighscoreService.Sdk.csproj HighscoreService.Sdk/
COPY HighscoreService.Hub.Infrastructure/HighscoreService.Hub.Infrastructure.csproj HighscoreService.Hub.Infrastructure/
COPY HighscoreService.Hub.Api/HighscoreService.Hub.Api.csproj HighscoreService.Hub.Api/
COPY HighscoreService.Screen.Infrastructure/HighscoreService.Screen.Infrastructure.csproj HighscoreService.Screen.Infrastructure/
COPY HighscoreService.Screen.Api/HighscoreService.Screen.Api.csproj HighscoreService.Screen.Api/
RUN dotnet restore HighscoreService.Hub.Api/HighscoreService.Hub.Api.csproj \
    && dotnet restore HighscoreService.Screen.Api/HighscoreService.Screen.Api.csproj

# Copy source and publish both Hub and Screen hosts into one image so CI/CD and Watchtower need no changes.
COPY . .
RUN dotnet publish HighscoreService.Hub.Api/HighscoreService.Hub.Api.csproj --configuration Release --no-restore --output /app/publish/hub /p:UseAppHost=false \
    && dotnet publish HighscoreService.Screen.Api/HighscoreService.Screen.Api.csproj --configuration Release --no-restore --output /app/publish/screen /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
ENV APP_DLL=hub/HighscoreService.Hub.Api.dll
WORKDIR /app

# Install curl for container health checks and pre-create /app/data for non-root SQLite writes.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data \
    && chown -R app:app /app/data

USER app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["sh", "-c", "cd \"$(dirname \"/app/$APP_DLL\")\" && exec dotnet \"/app/$APP_DLL\""]

