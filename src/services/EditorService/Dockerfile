# ---------- Stage 1: build ----------
# The SDK image has the compiler. It is only used to build and is not part of the final image.
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Copy only the project files first and restore.
# Docker caches this layer, so packages are only downloaded again when a .csproj changes.
COPY EditorService.Api/EditorService.Api.csproj EditorService.Api/
COPY EditorService.Application/EditorService.Application.csproj EditorService.Application/
COPY EditorService.Domain/EditorService.Domain.csproj EditorService.Domain/
COPY EditorService.Facade/EditorService.Facade.csproj EditorService.Facade/
COPY EditorService.Infrastructure/EditorService.Infrastructure.csproj EditorService.Infrastructure/
COPY EditorService.Sdk/EditorService.Sdk.csproj EditorService.Sdk/
RUN dotnet restore EditorService.Api/EditorService.Api.csproj

# Copy the rest of the source code and publish a Release build.
COPY . .
RUN dotnet publish EditorService.Api/EditorService.Api.csproj --configuration Release --no-restore --output /app/publish

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app

# curl is needed by the health check. Installed as root, before switching user.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as the built-in non-root user, so the container has fewer rights (security).
USER app

COPY --from=build /app/publish .

# .NET listens on port 8080 inside the container by default.
EXPOSE 8080

# Docker calls /health every 30 seconds. After 3 failures the container is marked "unhealthy".
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1
    
ENTRYPOINT ["dotnet", "EditorService.Api.dll"]