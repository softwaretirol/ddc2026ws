# Security-Review vor dem Commit

Diese Anleitung liest der Pre-Commit-Hook in `.claude/settings.json`. Er läuft, bevor Claude Code einen `git commit` ausführt. Das Team pflegt die Kriterien hier.

## Auftrag

1. **Arbeitsverzeichnis bestimmen:** Nimm `cwd` aus der Hook-Eingabe. Steht im Commit-Befehl `git -C <pfad>`, nimm stattdessen diesen Pfad.
2. **Änderungen ermitteln:** `git diff --cached` in diesem Verzeichnis.
   - Enthält der Befehl `-a`, `--all` oder `-am`, nimm zusätzlich `git diff` (ungestagte Änderungen an versionierten Dateien).
   - Ist der Diff leer, ist die Prüfung bestanden (`ok: true`).
3. **Nur die Änderungen bewerten:** Bewerte die geänderten Zeilen und ihren direkten Kontext. Lies dafür bei Bedarf die betroffenen Dateien. Bestehende Befunde, die der Commit nicht berührt, zählen nicht.
4. **Gründlich und knapp:** Gib keine Befunde aus, die du nicht am Diff belegen kannst.

## Kontext der Solution

- .NET 11:
  - Blazor WebAssembly mit MudBlazor in `src/ConferenceApp.Client`
  - Minimal APIs mit EF Core und PostgreSQL in `src/ConferenceApp.Api`
  - DTOs mit DataAnnotations in `src/ConferenceApp.Shared`
  - Aspire-AppHost
- **Bekannt und akzeptiert:** Keine Authentifizierung im MVP, alle Endpunkte und `/admin` sind anonym. Eine neue anonyme Route im bestehenden Muster ist deshalb höchstens eine Warnung (Mittel). Kritisch ist es erst, wenn ein Commit bestehende Schutzmechanismen entfernt oder abschwächt.

## Prüfpunkte und Schwere

### Kritisch – Commit blockieren
- **Secrets im Code:** Passwörter, API-Keys, Tokens, Connection-Strings mit Passwort, private Schlüssel oder Zertifikate. Das gilt auch in `appsettings*.json`, Tests und Skripten.
- **SQL-Injection:** `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQueryRaw` oder `NpgsqlCommand` mit String-Verkettung oder Interpolation von Eingaben.
- **XSS:** `MarkupString` bzw. `(MarkupString)` mit nicht vertrauenswürdigen Daten, oder JS-Interop, das Eingaben per `innerHTML` bzw. `eval` ausgibt.
- **Code- oder Befehlsausführung mit Eingaben:** `Process.Start`, Deserialisierung beliebiger Typen (`TypeNameHandling`, `BinaryFormatter`).
- **Entfernte Schutzmaßnahmen:** zum Beispiel Validierung, Escaping im `IcsBuilder` oder die Schema-Prüfung von Links.

### Hoch – Commit blockieren
- **CORS:** `AllowAnyOrigin`, `SetIsOriginAllowed(_ => true)` oder `AllowCredentials` zusammen mit einer weiten Origin-Liste.
- **Neue Eingabefelder ohne Grenzen:** Strings oder Listen ohne `MaxLength` bzw. Anzahlgrenze, URLs ohne Prüfung auf `http`/`https` (zum Beispiel `javascript:`), Track-Farben ohne die Regex `^#[0-9a-fA-F]{6}$`.
- **Daten in `href`, `src` oder `style`:** Nutzer- oder API-Daten landen dort ohne Schema- bzw. Formatprüfung.
- **Open Redirect:** `NavigateTo` oder `Redirect` mit einer URL aus Query oder Daten, ohne Beschränkung auf relative Pfade.
- **Header-Injection:** Eingaben in Response-Headern ohne Bereinigung, zum Beispiel `Content-Disposition`.
- **Zugriff ohne Zugehörigkeitsprüfung:** Ressourcen per ID ändern oder löschen, ohne zu prüfen, ob sie zur angegebenen Konferenz gehören (IDOR-Muster der Endpunkte).
- **Fehlerdetails in Produktion:** Stacktraces oder Exception-Texte in Antworten außerhalb von Development, `UseDeveloperExceptionPage` ohne Umgebungsprüfung, `EnableSensitiveDataLogging` ohne Umgebungsprüfung.
- **Unsichere Pakete:** neue NuGet- oder npm-Pakete mit bekannter Schwachstelle oder aus fremder Quelle.

### Mittel – nur warnen
- neue anonyme Endpunkte im bestehenden Muster (siehe Kontext)
- fehlende Security-Header, HTTP statt HTTPS in neuer Konfiguration
- unbehandelte `DbUpdateException` bzw. Race Conditions, die zu HTTP 500 führen
- externe Ressourcen (Bilder, Skripte, Fonts von Drittanbietern) mit Datenschutzfolgen
- Logging personenbezogener Daten

### Niedrig – nur warnen
- `AllowedHosts: "*"` und ähnliche Härtungsdetails
- fehlende `rel="noopener noreferrer"` bei `target="_blank"`
- IDE- oder Tool-Dateien mit lokalen Pfaden oder Verbindungsdaten im Commit (`.idea/**/dataSources*.xml` und Ähnliches)

## Ergebnis

- **Mindestens ein Befund Kritisch oder Hoch:** `ok: false`. Die Begründung enthält je Befund:
  - Schwere
  - Datei:Zeile
  - Problem in einem Satz
  - konkreten Fix

  Am Ende steht der Hinweis: „Bewusst umgehen nur nach Rücksprache mit dem Nutzer.“
- **Sonst:** `ok: true`. Gibt es Befunde der Stufe Mittel oder Niedrig, nenne sie in der Begründung als Warnung, im selben Format. Ohne Befunde lautet die Begründung „Security-Review: keine Befunde“.
- **Prüfung nicht möglich** (Git-Fehler, Verzeichnis unklar): `ok: false` mit der Ursache. Lieber einmal zu oft nachfragen, als ungeprüft committen.
