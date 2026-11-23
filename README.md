# ConferenceApp

Eine kleine Webanwendung, mit der man Entwicklerkonferenzen verwaltet und ihre Agenda veröffentlicht. Sie verwaltet mehrere Konferenzen, jeweils mit Tracks, Räumen, Sessions und Speakern. Die Agenda wird öffentlich als Raster angezeigt; zum Pflegen der Daten gibt es einen Admin-Bereich.

**Stack:** .NET 11 · Blazor WebAssembly mit MudBlazor · ASP.NET Core Minimal APIs · EF Core mit PostgreSQL · .NET Aspire 17

Der Umsetzungsplan steht in [docs/plans/2026-11-23-mvp-konferenz-app.md](docs/plans/2026-11-23-mvp-konferenz-app.md).

## Voraussetzungen

- .NET SDK 11.0.100 oder neuer
- Docker Desktop muss laufen, denn Aspire startet Postgres und pgAdmin als Container.

## Starten

```powershell
dotnet run --project src/ConferenceApp.AppHost --launch-profile http
```

**Hinweis zu HTTPS:** Beim ersten Start versucht Aspire, das HTTPS-Entwicklerzertifikat als vertrauenswürdig einzutragen. Unter Windows erscheint dazu ein Dialog „Sicherheitswarnung“, und bis zu einer Antwort wartet der Start. Du hast zwei Möglichkeiten:

- **Einmalig vertrauen:** `dotnet dev-certs https --trust` ausführen.
- **Ohne Zertifikat starten:** Die App nutzt lokal ohnehin nur HTTP. Dann so starten:

  ```powershell
  $env:ASPIRE_CLI_GENERATE_HTTPS_CERTIFICATE = 'false'
  $env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = 'true'
  dotnet run --project src/ConferenceApp.AppHost --launch-profile http
  ```

Nach dem Start sind diese Adressen erreichbar:

| Dienst | URL |
|---|---|
| Aspire-Dashboard | http://localhost:15283 (den Link mit Login-Token gibt die Konsole aus) |
| Client (öffentlich) | http://localhost:5226 |
| Admin-Bereich | http://localhost:5226/admin |
| API | http://localhost:5198 (OpenAPI unter `/openapi/v1.json`) |

Beim Start der API passiert automatisch Folgendes:
- Die EF-Core-Migrationen werden angewendet.
- Ist die Datenbank leer, wird die Demo-Konferenz „Dev Conference 2026“ eingespielt.

## Projektstruktur

```
src/
  ConferenceApp.AppHost/          Aspire: Postgres und pgAdmin, API, Client
  ConferenceApp.ServiceDefaults/  Health Checks, OpenTelemetry, Resilience
  ConferenceApp.Api/              Minimal APIs (Endpoints/), EF Core (Data/), Migrationen
  ConferenceApp.Client/           Blazor WASM: Pages/ (öffentlich), Pages/Admin/, Components/
  ConferenceApp.Shared/           DTOs und Requests mit Validierung (DataAnnotations)
```

## API im Überblick

| Bereich | Endpunkte |
|---|---|
| Konferenzen | `/api/conferences` mit CRUD (anlegen, lesen, ändern, löschen) |
| Öffentliche Agenda | `GET /api/conferences/{slug}/agenda` |
| Tracks, Räume, Sessions | `/api/conferences/{id}/tracks`, `/rooms` und `/sessions`, jeweils mit CRUD |
| Speaker | `/api/speakers` mit CRUD; `GET /api/speakers/{id}` liefert den Speaker samt Sessions |

Diese Regeln prüft die API:

| Regel | Antwort bei Verstoß |
|---|---|
| Eine Session endet nach ihrem Beginn. | 400 |
| Eine Session liegt innerhalb der Konferenztage. | 400 |
| Im selben Raum überschneiden sich keine Sessions. | 400 |
| Raum und Track gehören zur selben Konferenz wie die Session. | 400 |
| Der Slug einer Konferenz ist eindeutig. | 400 |
| Ein Raum, dem noch Sessions zugeordnet sind, kann nicht gelöscht werden. | 409 |

Fehler liefert die API als ProblemDetails.

## Entwicklung

**Neue Migration anlegen:**

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/ConferenceApp.Api --output-dir Data/Migrations
```

**Adresse der API ändern:** Der Client liest sie aus `src/ConferenceApp.Client/wwwroot/appsettings.json` (Eintrag `ApiBaseUrl`). Den CORS-Origin des Clients setzt der AppHost automatisch.

## Bewusst nicht im MVP

- Authentifizierung: `/admin` ist offen erreichbar.
- Call for Papers
- Teilnehmerregistrierung
- Persönliche Agenda
- Automatisierte Tests
