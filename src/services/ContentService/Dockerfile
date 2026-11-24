FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Copy and restore the .csproj before copying all source files so Docker reuses the NuGet restore cache whenever dependencies are unchanged.
COPY ContentService.Domain/ContentService.Domain.csproj ContentService.Domain/
RUN dotnet restore ContentService.Domain/ContentService.Domain.csproj

# Build with --no-restore in Release configuration rather than dotnet publish because Domain is a library artifact, not a standalone executable host.
COPY . .
RUN dotnet build ContentService.Domain/ContentService.Domain.csproj --no-restore --configuration Release
