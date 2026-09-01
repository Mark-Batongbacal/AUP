FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Tuki.Admin/Tuki.Admin.csproj Tuki.Admin/
RUN dotnet restore Tuki.Admin/Tuki.Admin.csproj

COPY Tuki.Admin/ Tuki.Admin/
RUN dotnet publish Tuki.Admin/Tuki.Admin.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --property UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=5030

COPY --from=build /app/publish .

EXPOSE 5030
ENTRYPOINT ["dotnet", "Tuki.Admin.dll"]
