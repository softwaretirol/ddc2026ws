# Plan: Container-Image und Veröffentlichung auf GitHub (ghcr.io)

- **Datum:** 2026-11-23
- **Status:** Umgesetzt, erster Workflow-Lauf im PR
- **Branch:** `feature/container-ghcr`

## 1. Ziel

Die ConferenceApp läuft als Container-Image. Ein GitHub-Workflow testet bei jedem Push auf `main`, baut das Image und veröffentlicht es in der GitHub Container Registry (ghcr.io).

## 2. Anforderungen und Entscheidungen

| Thema | Entscheidung |
|---|---|
| Aufbau | **Ein Image:** Die API liefert auch den Blazor-WebAssembly-Client aus. Damit laufen API und Oberfläche unter derselben Adresse, und CORS entfällt im Container. |
| Veröffentlichen | Bei **jedem Push auf `main`**: Tags `latest` und `sha-<kurz>`. Pull Requests bauen das Image nur zur Kontrolle, sie veröffentlichen nichts. |
| Gate | **Unit- und E2E-Tests** (Testcontainers, Playwright) müssen grün sein, sonst wird nichts veröffentlicht. |
| Registry | `ghcr.io/softwaretirol/ddc2026ws`. Anmeldung über `GITHUB_TOKEN` mit `packages: write`, ohne zusätzliche Secrets. |
| Datenbank | Nicht im Image. Der Connection-String kommt per Umgebungsvariable `ConnectionStrings__conferencedb`. Zum lokalen Testen gibt es `compose.yaml` mit Postgres. |
| Lokale Entwicklung | Bleibt unverändert mit Aspire; API und Client laufen dort weiterhin getrennt. |

## 3. Nicht im Umfang

- Deployment in eine Zielumgebung (Kubernetes, Azure usw.)
- Versionierung über Git-Tags und Releases
- HTTPS im Container: TLS übernimmt ein vorgeschalteter Reverse Proxy oder Ingress
- Härtungen aus der Security-Analyse (Anmeldung, Seeding nur in Development usw.). Die stehen als Hinweis in der README.

## 4. Technologie und Architektur

- **Dockerfile** (mehrstufig, im Repo-Root):
  - `sdk:11.0` veröffentlicht API und Client.
  - `aspnet:11.0` ist das Laufzeit-Image, mit dem Nicht-Root-Benutzer `app` und Port 8080.
  - Der Client landet als `wwwroot` neben der API.
  - Im Image ersetzt eine eigene `appsettings.json` des Clients die API-Adresse durch einen leeren Wert, damit der Client dieselbe Adresse verwendet.
- **API (`Program.cs`):** Ist `wwwroot/index.html` vorhanden, liefert die API den Client aus:
  - statische Dateien mit WASM-MIME-Typen
  - Fallback auf `index.html` für Client-Routen
  - `/api/*` ohne passenden Endpunkt bleibt 404
- **Client (`Program.cs`):** Ist `ApiBaseUrl` leer oder fehlt, nimmt er die eigene Basisadresse (gleicher Origin). In der Entwicklung bleibt `http://localhost:5198/` aus `appsettings.json`.
- **Workflow (`.github/workflows/container.yml`):**
  - Job `test`:
    - .NET 11
    - Build
    - Playwright-Chromium installieren
    - `dotnet test` für die ganze Solution
    - bei Fehlschlag Testergebnisse als Artefakt
  - Job `image`, abhängig von `test`:
    - Buildx
    - Metadaten (Tags und OCI-Labels)
    - Build
    - Push nur bei `push` auf `main`
    - GitHub-Actions-Cache für Layer
- **`.dockerignore`:** `bin/`, `obj/`, Tests, IDE-Dateien und `.git` bleiben draußen.

## 5. Umsetzungsschritte

- [x] Client: leere `ApiBaseUrl` nimmt die Basisadresse
- [x] API: Client ausliefern, wenn vorhanden, mit SPA-Fallback und 404 für `/api/*`
- [x] `Dockerfile`, `.dockerignore`, `compose.yaml`
- [x] Lokal prüfen:
  - Image bauen
  - mit `compose.yaml` starten
  - Startseite, Agenda und API im Browser bzw. per curl
- [x] Workflow `.github/workflows/container.yml`
- [x] Unit- und E2E-Tests lokal grün
- [x] README: Abschnitt „Container“
- [x] Commit (Security-Hook) und PR. Erster Workflow-Lauf im PR: Tests und Image-Build ohne Push.

## 6. Annahmen

- Für .NET 11 gibt es die Images `mcr.microsoft.com/dotnet/sdk:11.0` und `aspnet:11.0`.
- Das WebAssembly-Publish braucht kein `wasm-tools`-Workload (kein AOT).
- Auf `ubuntu-latest` stehen Docker (für Testcontainers) und `pwsh` (für die Playwright-Installation) bereit.
- Das Paket auf ghcr.io ist nach dem ersten Push privat. Die Sichtbarkeit stellt der Repo-Besitzer in den Paket-Einstellungen um.

## 7. Spätere Ausbaustufen

- Versions-Tags (`v1.2.3`) mit semantischen Image-Tags
- Multi-Arch-Images (arm64)
- Image-Scan (Trivy o. Ä.) und SBOM bzw. Signatur (cosign)
- Healthcheck-Endpunkt auch außerhalb von Development
- Seeding der Demo-Daten nur in Development
