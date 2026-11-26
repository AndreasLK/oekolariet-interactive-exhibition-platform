FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Restore dependencies using project files first for Docker layer caching.
COPY ContentService.Domain/ContentService.Domain.csproj ContentService.Domain/
COPY ContentService.Application/ContentService.Application.csproj ContentService.Application/
COPY ContentService.Facade/ContentService.Facade.csproj ContentService.Facade/
COPY ContentService.Infrastructure/ContentService.Infrastructure.csproj ContentService.Infrastructure/
COPY ContentService.Api/ContentService.Api.csproj ContentService.Api/
RUN dotnet restore ContentService.Api/ContentService.Api.csproj

# Copy source and publish the Release API artifact.
COPY . .
RUN dotnet publish ContentService.Api/ContentService.Api.csproj --no-restore --configuration Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Run as non-root user provided by the official .NET 11 ASP.NET runtime image.
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ContentService.Api.dll"]
