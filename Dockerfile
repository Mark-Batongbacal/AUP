FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/backend.csproj backend/
RUN dotnet restore backend/backend.csproj

COPY backend/ backend/
RUN dotnet publish backend/backend.csproj -c Debug -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Development
# Render containers can fail while .NET creates native file watchers for appsettings.json.
# Polling avoids that startup crash; production configuration is supplied through Render env vars.
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

COPY --from=build /app/publish .

EXPOSE 10000
ENTRYPOINT ["dotnet", "backend.dll"]
