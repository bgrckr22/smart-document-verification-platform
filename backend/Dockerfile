FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

COPY backend/src/SmartDocumentPlatform.Api/SmartDocumentPlatform.Api.csproj backend/src/SmartDocumentPlatform.Api/
RUN dotnet restore backend/src/SmartDocumentPlatform.Api/SmartDocumentPlatform.Api.csproj

COPY backend/src/SmartDocumentPlatform.Api backend/src/SmartDocumentPlatform.Api
RUN dotnet publish backend/src/SmartDocumentPlatform.Api/SmartDocumentPlatform.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/uploads \
    && chown -R "$APP_UID:$APP_UID" /app
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SmartDocumentPlatform.Api.dll"]
