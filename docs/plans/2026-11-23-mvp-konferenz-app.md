# Plan: Konferenz-App (MVP)

- **Datum:** 2026-11-23
- **Status:** Umgesetzt (2026-11-23)

## 1. Ziel

Eine kleine Webanwendung, mit der man Entwicklerkonferenzen verwaltet und ihre Agenda veröffentlicht. Der Umfang ist bewusst auf ein MVP beschränkt.

## 2. Anforderungen aus dem Interview

| Thema | Entscheidung |
|---|---|
| Rollen | Organisator/Admin, Speaker, Teilnehmer, öffentlicher Besucher. Im MVP gibt es dafür nur zwei Bereiche: öffentliche Seiten und `/admin`. |
| Kernfunktion | Konferenzen und ihre Agenda verwalten |
| Mandanten | Mehrere Konferenzen, z. B. DDC 2026 und DDC 2027 |
| Authentifizierung | Keine im MVP |
| Admin-Schutz | Keiner. `/admin` ist frei erreichbar, Auth folgt später. |
| Agenda | Tracks, Räume und Zeitslots |
| Speaker | Der Admin pflegt sie und ordnet sie Sessions zu (n:m) |
| UI-Framework | MudBlazor |
| Hosting lokal | .NET Aspire AppHost |
| Tests | Keine |

### Nicht im MVP

- Call for Papers
- Teilnehmerregistrierung und Ticketing
- Persönliche Agenda und Favoriten
- Login und Rollenprüfung
- Automatisierte Tests

## 3. Technologie

- .NET 11 (SDK 11.0.100)
- Blazor WebAssembly als eigenständiger Client, mit MudBlazor
- ASP.NET Core Minimal APIs
- EF Core mit dem Postgres-Provider Npgsql
- .NET Aspire für die lokale Orchestrierung: Postgres-Container, pgAdmin, API und Client
- Docker, damit Aspire den Postgres-Container starten kann

## 4. Projektstruktur

```
ConferenceApp.slnx
src/
  ConferenceApp.AppHost/          Aspire: startet Postgres-Container, pgAdmin, die API und den Client
  ConferenceApp.ServiceDefaults/  Aspire-Standards: Health Checks, OpenTelemetry
  ConferenceApp.Api/              Minimal APIs, EF Core, Migrations, Seed-Daten
  ConferenceApp.Client/           Blazor WASM mit MudBlazor
  ConferenceApp.Shared/           DTOs und Validierung, von API und Client gemeinsam genutzt
```

## 5. Datenmodell

| Entität | Felder | Hinweis |
|---|---|---|
| Conference | Id, Name, Slug, Description, StartDate, EndDate, Location | Der Slug ist eindeutig. |
| Track | Id, ConferenceId, Name, Color | Gehört zu einer Konferenz. |
| Room | Id, ConferenceId, Name, Capacity | Gehört zu einer Konferenz. |
| Speaker | Id, Name, Bio, Company, PhotoUrl, Links | Global: ein Speaker kann auf mehreren Konferenzen sprechen. |
| Session | Id, ConferenceId, Title, Abstract, StartTime, EndTime, RoomId, TrackId (optional) | |
| SessionSpeaker | SessionId, SpeakerId | Verknüpfungstabelle für die n:m-Beziehung |

### Regeln, die die API prüft

- Eine Session endet nach ihrem Beginn.
- Eine Session liegt innerhalb der Konferenztage.
- Im selben Raum dürfen sich zwei Sessions nicht überschneiden.

## 6. API (Minimal APIs)

| Bereich | Endpunkte |
|---|---|
| Konferenzen | `/api/conferences` mit CRUD (anlegen, lesen, ändern, löschen) |
| Öffentliche Agenda | `GET /api/conferences/{slug}/agenda` |
| Tracks | `/api/conferences/{id}/tracks` mit CRUD |
| Räume | `/api/conferences/{id}/rooms` mit CRUD |
| Sessions | `/api/conferences/{id}/sessions` mit CRUD |
| Speaker | `/api/speakers` mit CRUD |

Für alle Endpunkte gilt:

- Fehler kommen einheitlich als ProblemDetails zurück, Validierungsfehler mit Status 400.
- Eine OpenAPI-Beschreibung ist eingebaut.
- Der Client ist per CORS freigegeben. Seine Adresse kommt aus Aspire.

## 7. Frontend (Blazor WASM mit MudBlazor)

### Öffentliche Seiten

- Liste der Konferenzen
- Agenda als Raster: Räume in den Spalten, Uhrzeiten in den Zeilen, pro Tag ein Tab, Filter nach Track
- Detailseite einer Session
- Detailseite eines Speakers

### Admin-Bereich `/admin`

- Tabellen und Dialog-Formulare für Konferenzen, Tracks, Räume, Speaker und Sessions
- Beim Anlegen einer Session wählt man Raum, Track und Speaker aus Listen aus.

## 8. Umsetzungsschritte

1. [x] Aspire-Templates installieren und die Projekte anlegen. AppHost mit Postgres-Container und pgAdmin einrichten.
2. [x] API: DbContext, Entitäten und erste Migration anlegen. Beim Start werden Migrationen automatisch angewendet und Demodaten eingespielt (eine Beispiel-Konferenz).
3. [x] API: Endpunkte und DTOs im Shared-Projekt anlegen, Validierung einbauen.
4. [x] Client: MudBlazor einrichten, Layout und Navigation bauen, einen typisierten HTTP-Client an die API anbinden.
5. [x] Client: öffentliche Seiten bauen (Konferenzen, Agenda-Raster, Details).
6. [x] Client: Admin-Seiten bauen (Tabellen und Formulare).
7. [x] Von Hand prüfen: AppHost starten und die wichtigsten Abläufe im Browser durchklicken. Danach README schreiben und committen.

## 9. Annahmen

Diese Punkte wurden im Interview nicht gefragt. Bitte melden, falls etwas nicht passt.

- **Projektname:** ConferenceApp
- **Sprache:** UI auf Deutsch, Code auf Englisch
- **Zeiten:** Alle Zeiten sind lokale Zeit der Konferenz. Zeitzonen behandelt das MVP nicht.
- **Fotos:** Speaker-Fotos werden nur als Link (URL) gespeichert, nicht hochgeladen.
- **Paket-Versionen:** Wo es für .NET 11 nur Vorabversionen gibt (Npgsql, Aspire, MudBlazor), nehmen wir die neueste passende Version.

## 10. Abweichungen bei der Umsetzung

- **Aspire 17:** Die Projekte werden mit `AddDotnetProject(name, path)` eingebunden, nicht mehr mit `AddProject<T>`. Der AppHost braucht deshalb keine Projektverweise.
- **EF Core:** Statt des Aspire-Pakets `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` nutzen wir direkt `Npgsql.EntityFrameworkCore.PostgreSQL 11.0.0`. Das Aspire-Paket gibt es für net11.0 nur als Vorabversion. Den Connection-String `conferencedb` liefert Aspire weiterhin.
- **Zeiten:** Sessions speichern `Day` (DateOnly) sowie `StartTime` und `EndTime` (TimeOnly). Das passt zur Annahme „lokale Zeit der Konferenz“.
- **Adresse der API im Client:** Sie steht in `wwwroot/appsettings.json`, Standard ist `http://localhost:5198/`. Der WASM-Client kann die Service Discovery von Aspire nicht nutzen.
- **Zusätzlich gebaut:** eine öffentliche Speaker-Liste unter `/speaker` und eine Suche in der Speaker-Verwaltung.
- **HTTPS-Entwicklerzertifikat:** Aspire will es beim Start als vertrauenswürdig eintragen. Die Umgehung über Umgebungsvariablen steht in der README.

## 11. Spätere Ausbaustufen

- Authentifizierung mit ASP.NET Core Identity und Rollen
- Call for Papers mit Review
- Teilnehmerregistrierung mit Kapazitätslimit
- Persönliche Agenda
- API-Integrationstests mit Testcontainers
