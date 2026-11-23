# syntax=docker/dockerfile:1
# ConferenceApp: ein Image – die ASP.NET-Core-API liefert auch den Blazor-WebAssembly-Client aus.
# Build:  docker build -t conferenceapp .
# Start:  siehe compose.yaml (braucht PostgreSQL über ConnectionStrings__conferencedb)

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Erst nur Projektdateien kopieren, damit der Restore-Layer bei Codeänderungen gecacht bleibt.
COPY global.json ./
COPY src/ConferenceApp.Shared/ConferenceApp.Shared.csproj src/ConferenceApp.Shared/
COPY src/ConferenceApp.ServiceDefaults/ConferenceApp.ServiceDefaults.csproj src/ConferenceApp.ServiceDefaults/
COPY src/ConferenceApp.Api/ConferenceApp.Api.csproj src/ConferenceApp.Api/
COPY src/ConferenceApp.Client/ConferenceApp.Client.csproj src/ConferenceApp.Client/
RUN dotnet restore src/ConferenceApp.Api/ConferenceApp.Api.csproj \
 && dotnet restore src/ConferenceApp.Client/ConferenceApp.Client.csproj

COPY src/ src/
RUN dotnet publish src/ConferenceApp.Api/ConferenceApp.Api.csproj -c Release -o /out/api --no-restore /p:UseAppHost=false \
 && dotnet publish src/ConferenceApp.Client/ConferenceApp.Client.csproj -c Release -o /out/client --no-restore

# Im Container ruft der Client die API über denselben Origin auf (leere ApiBaseUrl, siehe Client/Program.cs).
RUN printf '{\n  "ApiBaseUrl": ""\n}\n' > /out/client/wwwroot/appsettings.json \
 && rm -f /out/client/wwwroot/appsettings.json.br /out/client/wwwroot/appsettings.json.gz

# ---------- Laufzeit ----------
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /out/api ./
COPY --from=build /out/client/wwwroot ./wwwroot

# Nicht als root laufen (Benutzer "app" ist im aspnet-Image vorhanden).
USER $APP_UID
ENTRYPOINT ["dotnet", "ConferenceApp.Api.dll"]
