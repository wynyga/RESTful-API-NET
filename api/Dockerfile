# syntax=docker/dockerfile:1
# Build context is the api/ folder:  docker build -t project-tracker-api api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on the project files alone, so this layer is cached until a dependency changes.
COPY API/API.csproj API/
COPY Business/Business.csproj Business/
COPY Data/Data.csproj Data/
COPY Models/Models.csproj Models/
RUN dotnet restore API/API.csproj

COPY API API
COPY Business Business
COPY Data Data
COPY Models Models
RUN dotnet publish API/API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "API.dll"]
