FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY ContentService.Domain/ContentService.Domain.csproj ContentService.Domain/
RUN dotnet restore ContentService.Domain/ContentService.Domain.csproj

COPY . .
RUN dotnet build ContentService.Domain/ContentService.Domain.csproj --no-restore --configuration Release

