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
tests/
  ConferenceApp.UnitTests/        xUnit: Validierung, Client-Logik (Format, ApiClient, Agenda, Konflikte, Kontrast), iCal, API-Filter/Mapping
  ConferenceApp.E2ETests/         xUnit + Testcontainers (Postgres) + Playwright: API über HTTP und UI im Browser
```

## API im Überblick

| Bereich | Endpunkte |
|---|---|
| Konferenzen | `/api/conferences` mit CRUD (anlegen, lesen, ändern, löschen) |
| Öffentliche Agenda | `GET /api/conferences/{slug}/agenda` |
| Agenda als Kalender (iCal, abonnierbar) | `GET /api/conferences/{slug}/agenda.ics`, optional `?track={trackId}` |
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

## Tests

Die Tests laufen mit xUnit v3 auf der Microsoft Testing Platform (eingestellt in `global.json`).

```powershell
dotnet test                                            # alle Tests der Solution
dotnet test --project tests/ConferenceApp.UnitTests    # nur Unit-Tests
dotnet test --project tests/ConferenceApp.E2ETests     # nur E2E-Tests (API + UI)
dotnet test --project tests/ConferenceApp.E2ETests --filter-namespace ConferenceApp.E2ETests.Api   # nur API-Tests
```

**Unit-Tests** (`tests/ConferenceApp.UnitTests`) brauchen weder Datenbank noch Netzwerk. Sie prüfen die Validierung der Requests, die Formatierung im Client, die Fehlerauswertung des `ApiClient`, Pausenerkennung (`AgendaTimeline`), Starttag und „Jetzt“ (`AgendaNow`), Farbkontrast (`ColorContrast`), Konfliktprüfung im Session-Dialog (`ScheduleConflicts`), den iCalendar-Export (`IcsBuilder`), den `ValidationFilter` und das Mapping der API.

**E2E-Tests** (`tests/ConferenceApp.E2ETests`) haben diese Voraussetzungen:

- **Docker Desktop muss laufen.** Pro Testlauf startet Testcontainers einen Postgres-Container und entfernt ihn danach wieder.
- **Playwright-Chromium:** Die Tests installieren den Browser beim ersten UI-Test selbst. Von Hand geht es nach dem Build so:

  ```powershell
  dotnet build tests/ConferenceApp.E2ETests
  pwsh tests/ConferenceApp.E2ETests/bin/Debug/net11.0/playwright.ps1 install chromium
  # ohne PowerShell 7:
  powershell -ExecutionPolicy Bypass -File tests/ConferenceApp.E2ETests/bin/Debug/net11.0/playwright.ps1 install chromium
  ```

So läuft ein E2E-Testlauf ab:

- Die API läuft in-process über `WebApplicationFactory` mit echtem Kestrel auf einem freien Port. `ConnectionStrings:conferencedb` zeigt dabei auf den Container, `ClientOrigin` auf den Test-Client. Beim Start werden Migrationen und Seed angewendet.
- Der Client wird beim ersten UI-Test per `dotnet publish -c Release` veröffentlicht und von einem kleinen statischen Kestrel-Host auf einem freien Port ausgeliefert, mit SPA-Fallback auf `index.html`. Ein bereits veröffentlichtes `wwwroot` kann man über die Umgebungsvariable `E2E_CLIENT_WWWROOT` vorgeben.
- Playwright beantwortet `appsettings.json` des Clients per Route, sodass `ApiBaseUrl` auf die Test-API zeigt.
- Jeder Test legt eigene Konferenzen mit eindeutigem Slug an. Die Seed-Konferenz „Dev Conference 2026“ wird nur gelesen.
- Schlägt ein UI-Test fehl, liegen Screenshot und Playwright-Trace unter `tests/ConferenceApp.E2ETests/bin/<Konfiguration>/net11.0/TestResults/playwright/`. Den Trace öffnet `pwsh bin/.../playwright.ps1 show-trace <datei>.trace.zip`.

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
