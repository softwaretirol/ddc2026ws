# Plan: UI- und UX-Verbesserungen der Konferenz-App

- **Datum:** 2026-11-23
- **Status:** Entwurf
- **Grundlage:** Playwright-Analyse von `/konferenzen/devconf-2026` (Desktop 1440 px, Mobil 390 px, Tastatur) und Durchsicht von `src/ConferenceApp.Client`

## 1. Ziel

Die öffentliche Agenda soll auf jedem Gerät gut lesbar und bedienbar sein, Besucher sollen schneller die passende Session finden, und Organisatoren sollen Sessions mit weniger Fehlversuchen planen. Der Plan bleibt im MVP-Rahmen: keine Anmeldung, kein CfP, keine Registrierung.

### Übersicht der 20 Punkte

| Nr. | Verbesserung | Bereich | Prio | Aufwand | Phase |
|---|---|---|---|---|---|
| 1 | Mobile Agenda als chronologische Liste | Agenda | Hoch | L | 2 |
| 2 | Programmlücken stauchen und als Pause beschriften | Agenda | Hoch | M | 2 |
| 3 | Filter in der URL, trackfreie Sessions sichtbar, `aria-pressed` | Agenda | Hoch | S | 2 |
| 4 | Semantik und Tastatur: `h1`, Skip-Link, Spaltenköpfe, Breadcrumb | Alle Seiten | Hoch | S | 1 |
| 5 | Eigenes Admin-Layout, schlanke öffentliche Navigation | Layout | Hoch | M | 1 |
| 6 | „Heute“ und „Jetzt“: richtiger Starttag, Jetzt-Linie, Als Nächstes | Agenda | Mittel | M | 2 |
| 7 | Lesbare Session-Karten mit Abstract-Vorschau | Agenda | Mittel | M | 2 |
| 8 | Kontrastsichere Track-Farben | Agenda, Admin | Hoch | S | 1 |
| 9 | Suche in der Agenda (Titel, Speaker) | Agenda | Mittel | S | 2 |
| 10 | Kalender-Export (iCal) pro Session und Konferenz | Session, Agenda | Mittel | M | 3 |
| 11 | Session-Detail mit Kontext und Client-Cache der Agenda | Session | Mittel | M | 3 |
| 12 | Startseite: Konferenzkarten mit Status und Reihenfolge | Startseite | Mittel | S | 3 |
| 13 | Speaker-Übersicht: Suche, Konferenzfilter, klickbare Karten | Speaker | Mittel | M | 3 |
| 14 | Lade-, Leer- und Fehlerzustände, Startbildschirm | Alle Seiten | Hoch | M | 1 |
| 15 | Session-Dialog: Konflikte sofort sehen, schnellere Zeiteingabe | Admin | Hoch | M | 4 |
| 16 | Admin-Sessionliste: Filter, Gruppierung, Hinweise | Admin | Mittel | M | 4 |
| 17 | Formulare: Fehler am Feld, ungespeicherte Änderungen, Komfort | Admin | Mittel | M | 4 |
| 18 | Einheitliche Seitenköpfe, Typografie, Abstände, Datumsformate | Alle Seiten | Mittel | S | 1 |
| 19 | Dark Mode | Alle Seiten | Niedrig | M | 3 |
| 20 | Admin-Löschaktionen absichern und Folgen vorab zeigen | Admin | Niedrig | S | 4 |

Aufwand: S = bis ½ Tag, M = 1–2 Tage, L = 3+ Tage.

## 2. Anforderungen und Entscheidungen

Je Punkt: Problem mit Beleg, Lösung, betroffene Dateien. Pfade relativ zu `src/ConferenceApp.Client/`, sofern nicht anders angegeben.

### Phase 1: Grundlagen (Layout, Zugänglichkeit, Zustände)

#### 4. Semantik und Tastatur – Hoch, S
- **Problem:** Keine Seite hat ein `<h1>`: Konferenzname ist `MudText Typo.h5` (`Pages/Agenda.razor`), App-Name `h6` (`Layout/MainLayout.razor`). `App.razor` setzt `FocusOnNavigate Selector="h1"` – der Fokus landet deshalb nach Navigation nirgends. 8× Tab durch die Navigation bis zum Inhalt. Aktueller Breadcrumb ist ein fokussierbarer Link mit `href="#"`. Raumnamen in `AgendaGrid.razor` sind `<div><strong>`, keine Überschriften.
- **Lösung:** Seitentitel als `h1` (über `PageHeader`, siehe Punkt 18), App-Name als normaler Link. Skip-Link „Zum Inhalt springen“ als erstes Element, Ziel `<main id="inhalt" tabindex="-1">`. Raumspalten mit `role="columnheader"` bzw. die Karten mit `aria-label` „Titel, Zeit, Raum“. Letzten Breadcrumb als Text mit `aria-current="page"` rendern (eigene kleine `Breadcrumbs`-Komponente oder `ItemTemplate`).
- **Dateien:** `App.razor`, `Layout/MainLayout.razor`, `Components/AgendaGrid.razor`, alle `Pages/*.razor`, `wwwroot/css/app.css`.

#### 5. Eigenes Admin-Layout, schlanke öffentliche Navigation – Hoch, M
- **Problem:** `MainLayout.razor` zeigt Besuchern einen auffälligen „Admin“-Button in der AppBar und die Gruppe „Verwaltung“ im Drawer. Der Drawer ist standardmäßig offen (`_drawerOpen = true`, 240 px) und nimmt dem Raster auf Desktop Platz; auf Mobil überdeckt er den Inhalt.
- **Lösung:** `PublicLayout`: AppBar mit Logo/Name und zwei Links (Konferenzen, Speaker), ab `md` horizontal, darunter Menü-Button mit temporärem Drawer. Kein Admin-Link sichtbar (nur Footer-Link „Verwaltung“, dezent). `AdminLayout`: eigene Farbe/Kennzeichnung „Verwaltung“, persistenter Drawer mit Konferenzen und Speaker, Link „Zur öffentlichen Seite“. Admin-Seiten per `@layout AdminLayout` (oder `_Imports.razor` im Ordner `Pages/Admin`).
- **Dateien:** `Layout/MainLayout.razor` → `Layout/PublicLayout.razor`, neu `Layout/AdminLayout.razor`, neu `Pages/Admin/_Imports.razor`, `App.razor`.

#### 8. Kontrastsichere Track-Farben – Hoch, S
- **Problem:** Track-Namen werden in der Track-Farbe auf Weiß gesetzt (`.agenda-track` mit 0,72 rem, Chips in `Agenda.razor`, Overline in `SessionDetail.razor`). Seed-Farben: `#E65100` ca. 3,8 : 1, `#00897B` ca. 4,3 : 1 – beide unter WCAG AA (4,5 : 1). Weiße Schrift auf aktivem Chip `#00897B` ebenfalls ca. 4,3 : 1. `TrackDialog.razor` erlaubt jede Farbe ohne Hinweis.
- **Lösung:** Farbe nur für Flächen und Rand (Randstreifen, Punkt) verwenden, Text immer in `text-primary`. Für aktive Chips Textfarbe per Luminanz wählen (Hilfsfunktion `ColorContrast.TextOn(hex)` in `Components/Format.cs` oder eigener Klasse). Im Track-Dialog Kontrastwert anzeigen und unter 3 : 1 (Randfarbe gegen Hintergrund) warnen. Seed-Farben abdunkeln (z. B. `#BF360C`, `#00796B`).
- **Dateien:** `Components/AgendaGrid.razor`, `Pages/Agenda.razor`, `Pages/SessionDetail.razor`, `Components/Dialogs/TrackDialog.razor`, neu `Components/ColorContrast.cs`, `src/ConferenceApp.Api/Data/SeedData.cs`.

#### 14. Lade-, Leer- und Fehlerzustände, Startbildschirm – Hoch, M
- **Problem:** `PageState.razor` zeigt beim Laden nur eine dünne `MudProgressLinear`, danach springt das Layout. Fehler sind ein `MudAlert` ohne „Erneut versuchen“. `Speakers.razor` hat keinen Leerzustand (leeres Grid). `Home.razor` verweist Besucher im Leerzustand auf den Admin-Bereich. `NotFound.razor` ist englisch („Not Found“, `h3`). `#blazor-error-ui` ist das gelbe Standardbanner. Der WASM-Start zeigt nur einen Prozentkreis ohne App-Rahmen.
- **Lösung:** `PageState` um `Skeleton`-Slot (MudSkeleton passend zu Karten/Raster) und `OnRetry` erweitern; Fehlertexte nach Ursache (Netzwerk vs. 404). Leerzustände mit Icon, Satz und – nur im Admin – Aktion. 404-Seite deutsch mit Links zu Konferenzen/Speaker. `index.html`: statische App-Shell (AppBar-Farbe, Name, Skelett) statt Kreis, damit der Start ruhig wirkt; Roboto selbst hosten statt Google Fonts (ein externer Request weniger, Datenschutz). Fehlerleiste im App-Stil.
- **Dateien:** `Components/PageState.razor`, `Pages/Home.razor`, `Pages/Speakers.razor`, `Pages/NotFound.razor`, `wwwroot/index.html`, `wwwroot/css/app.css`, `Services/ApiClient.cs` (Netzwerkfehler unterscheidbar machen).

#### 18. Einheitliche Seitenköpfe, Typografie, Abstände, Datumsformate – Mittel, S
- **Problem:** Überschriften uneinheitlich: `h4` auf `Home`, `Speakers`, `AdminConferences`, `AdminSpeakers`, `AdminConference`, `SessionDetail`, `SpeakerDetail`; `h5` in der Agenda. Abstände mal `pa-6`, mal `mb-4`, mal CSS-Klassen; Inline-Styles in Razor (`Style="border-left:6px …"`). Theme setzt nur Farben (`MainLayout.razor`), keine Typografie. Tagesbuttons zeigen „Mo., 25.05.2026“ (lang, auf Mobil zweizeilig).
- **Lösung:** Komponente `PageHeader` (Breadcrumb, `h1`, Untertitel/Meta, Aktionsbereich rechts) für alle Seiten. Typografie im `MudTheme` festlegen (h1 ca. 1,75 rem öffentlich, 1,5 rem Admin). Abstands-Skala dokumentieren (8/16/24) und Inline-Styles in CSS-Klassen überführen. `Format` um `DayShort` („Mo 25.05.“) für Tabs/Listen erweitern, `DateRange` im selben Monat als „25.–27.05.2026“.
- **Dateien:** neu `Components/PageHeader.razor`, alle `Pages/**/*.razor`, `Layout/*`, `Components/Format.cs`, `wwwroot/css/app.css`.

### Phase 2: Agenda für Besucher

#### 1. Mobile Agenda als chronologische Liste – Hoch, L
- **Problem:** Auf 390 px ist das Raster 1005 px breit bei 358 px Viewport; nur eine Raumspalte sichtbar, die Zeitspalte scrollt mit weg (`AgendaGrid.razor`: `minmax(190px, 1fr)` pro Raum, `.agenda-scroll` mit `overflow-x:auto`).
- **Lösung:** Unter 768 px (`MudBreakpointProvider`/`MudHidden` oder CSS-Container-Query) neue Komponente `AgendaList`: Gruppen je Startzeit („09:00“ als sticky Kopf), darin Karten mit Raum, Titel, Speaker, Track. Pausen aus Punkt 2 als eigene Zeile. Auf Desktop bleibt das Raster; dort Zeitspalte `position: sticky; left: 0`. Optional Umschalter „Raster/Liste“ auch auf Desktop (in URL, siehe Punkt 3).
- **Dateien:** neu `Components/AgendaList.razor`, `Components/AgendaGrid.razor`, `Pages/Agenda.razor`, `wwwroot/css/app.css`.

#### 2. Programmlücken stauchen und als Pause beschriften – Hoch, M
- **Problem:** Lücke 11:15–13:00 ergibt ca. 230 px leeres Raster; unklar, ob Pause oder fehlende Daten. Das Raster rechnet linear (`Row(t)` in `AgendaGrid.razor`).
- **Lösung:** Beim Aufbau der Zeilen Zeiträume ohne Session in allen Räumen ab 30 min erkennen und auf eine feste Höhe (ca. 32 px) stauchen; die Zeile über alle Räume beschriften „Pause · 11:15–13:00“. `Row()` auf eine Zeitachsen-Abbildung (Liste von Segmenten) umstellen. Pausen als echte Einträge (Mittagessen, Kaffee) erfordern ein Datenmodell-Feld und sind unter „Spätere Ausbaustufen“ geführt.
- **Dateien:** `Components/AgendaGrid.razor`, `Components/AgendaList.razor`, neu `Components/AgendaTimeline.cs` (gemeinsame Berechnung von Slots und Lücken).

#### 3. Filter in der URL, trackfreie Sessions sichtbar, `aria-pressed` – Hoch, S
- **Problem:** `_day` und `_trackFilter` sind nur Felder in `Agenda.razor`: nicht teilbar, nach Reload weg. `SessionsFor` filtert `s.TrackId == _trackFilter` und blendet damit Keynote und Closing Panel (ohne Track) aus. Track-Chips haben kein `aria-pressed`.
- **Lösung:** `[SupplyParameterFromQuery]` für `tag` (yyyy-MM-dd), `track` (Name-Slug oder Id), `q` (Punkt 9), `ansicht`. Änderungen per `NavigationManager.NavigateTo(GetUriWithQueryParameters(...), replace: true)`. Sessions ohne Track bei aktivem Filter weiter zeigen, gekennzeichnet „Für alle“. Chips mit `aria-pressed`, Gruppe mit `role="group" aria-label="Track-Filter"`. Optional Mehrfachauswahl.
- **Dateien:** `Pages/Agenda.razor`.

#### 6. „Heute“ und „Jetzt“ – Mittel, M
- **Problem:** `Agenda.razor` setzt `_day = _agenda.Conference.StartDate` – auch am zweiten Konferenztag startet die Ansicht mit Tag 1. Es gibt keinen Hinweis, was gerade läuft.
- **Lösung:** Starttag: Query-Parameter, sonst heute (falls innerhalb der Konferenz), sonst Tag 1. Am laufenden Tag: horizontale „Jetzt“-Linie im Raster, laufende Sessions hervorgehoben, über der Agenda eine kompakte Leiste „Läuft gerade“ / „Als Nächstes“ mit Sprunglinks. Aktualisierung per `PeriodicTimer` (1 min). Zeit kommt aus der Browser-Uhr (siehe Annahmen).
- **Dateien:** `Pages/Agenda.razor`, `Components/AgendaGrid.razor`, `Components/AgendaList.razor`, neu `Components/NowNext.razor`.

#### 7. Lesbare Session-Karten mit Abstract-Vorschau – Mittel, M
- **Problem:** Raster mit 11 px pro 5 min (`RowHeight = 11`): eine 15-min-Session ist 33 px hoch, minus Rand/Padding bleibt nur die Zeitzeile; `overflow:hidden` schneidet Titel ab. Karten zeigen keinen Abstract, man muss jede Session öffnen. Fokuszustand der Karten-Links ist nicht sichtbar gestaltet (nur `:hover`).
- **Lösung:** Zeilenhöhe auf 14 px (bzw. abhängig von der kürzesten Session) erhöhen – Punkt 2 gleicht die Gesamthöhe aus. Titel mit `-webkit-line-clamp`, volle Angaben im `title`/`aria-label`. Abstract als Zweizeiler, wenn Platz ist; sonst Quick-View: Klick öffnet `MudDialog`/Drawer mit Abstract, Speakern und Link zur Detailseite (Detailseite bleibt direkt verlinkbar). `:focus-visible`-Stil wie `:hover`.
- **Dateien:** `Components/AgendaGrid.razor`, `Components/AgendaList.razor`, neu `Components/SessionQuickView.razor`, `wwwroot/css/app.css`.

#### 9. Suche in der Agenda – Mittel, S
- **Problem:** Es gibt nur Tag- und Track-Filter. Wer eine Speakerin oder ein Thema sucht, muss alle Tage durchsehen. Eine Suche existiert nur in `AdminSpeakers.razor`.
- **Lösung:** Suchfeld in der Toolbar (clientseitig, alle Daten liegen in `AgendaDto` vor), sucht in Titel, Abstract, Speaker-Namen über alle Tage. Treffer auf anderen Tagen als Zähler an den Tagesbuttons („Di · 2“). Suchbegriff in der URL (`q`).
- **Dateien:** `Pages/Agenda.razor`.

### Phase 3: Details und Entdecken

#### 10. Kalender-Export (iCal) – Mittel, M
- **Problem:** Ohne persönliche Agenda (bewusst nicht im MVP) haben Besucher keinen Weg, Sessions in ihren Kalender zu übernehmen.
- **Lösung:** Button „Zum Kalender“ auf Session-Detail und im Quick-View: `.ics` clientseitig erzeugen und per kleinem JS-Interop (`Blob`-Download) ausliefern. Für die ganze Konferenz neuer API-Endpunkt `GET /api/conferences/{slug}/agenda.ics` (abonnierbar, optional mit `?track=`). Zeitzone: siehe Annahmen.
- **Dateien:** neu `Components/IcsBuilder.cs`, neu `wwwroot/js/download.js`, `Pages/SessionDetail.razor`, `Pages/Agenda.razor`; API: `src/ConferenceApp.Api/Endpoints/ConferenceEndpoints.cs`.

#### 11. Session-Detail mit Kontext und Client-Cache – Mittel, M
- **Problem:** `SessionDetail.razor` lädt die komplette Agenda, um eine Session zu zeigen; zurück auf der Agenda wird sie erneut geladen und Tag/Filter sind zurückgesetzt. Die Seite bietet keinen Rückweg außer Breadcrumb und keine Orientierung (was läuft parallel, was danach im Raum).
- **Lösung:** `AgendaCache` (scoped Service, hält `AgendaDto` je Slug für einige Minuten). Auf der Detailseite: „← Zur Agenda“ mit Tag/Filter aus der Herkunfts-URL, Abschnitt „Parallel“ (gleiche Zeit, andere Räume) und „Danach in diesem Raum“, Buttons „Teilen“ (Web Share API, Fallback Link kopieren) und „Zum Kalender“ (Punkt 10). Speaker-Karten ganz klickbar.
- **Dateien:** neu `Services/AgendaCache.cs`, `Program.cs`, `Pages/SessionDetail.razor`, `Pages/Agenda.razor`.

#### 12. Startseite: Konferenzkarten mit Status – Mittel, S
- **Problem:** `Home.razor` listet Konferenzen nach `StartDate` absteigend (API), ohne Unterschied zwischen vergangen, laufend und bevorstehend. Beschreibung wird ungekürzt gezeigt; nur der Button „Agenda“ ist klickbar.
- **Lösung:** Abschnitte „Läuft gerade“, „Demnächst“ (aufsteigend), „Vergangen“ (eingeklappt). Status-Chip und „in 12 Tagen“. Beschreibung auf 3 Zeilen begrenzen, ganze Karte als Link (`MudCard` in `<a>` bzw. Stretched-Link), Kennzahlen „3 Tage · 24 Sessions“ – dafür `SessionCount` in `ConferenceDto` ergänzen (optional).
- **Dateien:** `Pages/Home.razor`, `wwwroot/css/app.css`; optional `src/ConferenceApp.Shared/Contracts.cs`, `src/ConferenceApp.Api/Endpoints/ConferenceEndpoints.cs`.

#### 13. Speaker-Übersicht: Suche, Konferenzfilter, klickbare Karten – Mittel, M
- **Problem:** `Speakers.razor` zeigt alle globalen Speaker (auch ohne Session) nur mit Name und Firma, ohne Suche oder Filter; nur der Name ist Link. `SpeakerDetail.razor` zeigt Links mit der vollen URL als Buttontext und `Target="_blank"` ohne Hinweis.
- **Lösung:** Suchfeld und Filter „Konferenz“ (Daten aus der Agenda der gewählten Konferenz, keine API-Änderung nötig), alphabetisch mit Buchstabenankern ab ca. 30 Speakern. Ganze Karte klickbar, Bio-Anriss (2 Zeilen). Auf der Detailseite Links mit Domain/Icon (GitHub, LinkedIn, Website), `rel="noopener"` und Hinweis „öffnet in neuem Tab“; Sessions nach Konferenz gruppiert, kommende zuerst.
- **Dateien:** `Pages/Speakers.razor`, `Pages/SpeakerDetail.razor`, `Components/SpeakerAvatar.razor`.

#### 19. Dark Mode – Niedrig, M
- **Problem:** `MainLayout.razor` definiert nur `PaletteLight`. In CSS und Razor fest verdrahtete Farben: `#9E9E9E` (Track-Fallback), `rgba(0,0,0,…)`-Schatten, `#e0e0e0`/`#3949AB` im Ladekreis, `color:#fff` auf Chips.
- **Lösung:** `PaletteDark` ergänzen, Systemeinstellung per `MudThemeProvider.GetSystemDarkModeAsync` übernehmen, Umschalter in der AppBar (Wahl in `localStorage`). Feste Farben durch `--mud-palette-*` ersetzen. Track-Farben bleiben, Kontrast über Punkt 8 abgesichert.
- **Dateien:** `Layout/*`, `wwwroot/css/app.css`, `Components/AgendaGrid.razor`, `Pages/Agenda.razor`, `wwwroot/index.html`.

### Phase 4: Admin-Werkzeuge

#### 15. Session-Dialog: Konflikte sofort sehen, schnellere Zeiteingabe – Hoch, M
- **Problem:** Raumkonflikte meldet erst die API nach „Speichern“ (`SessionEndpoints.ApplyAsync`), als Text oben im Dialog. Doppelt gebuchte Speaker (zwei Räume zur selben Zeit) prüft niemand. Neue Sessions starten immer mit Tag 1, 09:00–10:00 (`SessionRequest`-Defaults); `MudTimePicker` braucht mehrere Klicks.
- **Lösung:** `AdminConference.razor` hat alle Sessions geladen – Konflikte clientseitig live prüfen und unter den Feldern anzeigen: Raum belegt (blockierend, wie API) und Speaker parallel eingeplant (Warnung, Speichern möglich). Kleine Tagesbelegung des gewählten Raums im Dialog (Zeitleiste mit freien Lücken). Zeiteingabe als Textfeld `HH:mm` mit Dauer-Chips (30/45/60 min) statt zweitem Picker. Vorbelegung: zuletzt genutzter Tag/Raum, Beginn = Ende der letzten Session im Raum. Optional API: Speaker-Konflikt als Warnung im Response.
- **Dateien:** `Components/Dialogs/SessionDialog.razor`, `Pages/Admin/AdminConference.razor`, neu `Components/ScheduleConflicts.cs`.

#### 16. Admin-Sessionliste: Filter, Gruppierung, Hinweise – Mittel, M
- **Problem:** `AdminConference.razor` zeigt alle Sessions in einer flachen `MudTable` ohne Filter, Suche oder Sortierung; bei 3 Tagen × 4 Räumen wird das lang. Lücken im Plan und unvollständige Sessions (ohne Speaker, ohne Track, ohne Abstract) fallen nicht auf.
- **Lösung:** Gruppierung nach Tag (`GroupBy`), Filter Raum/Track, Suchfeld, sortierbare Spalten. Hinweis-Icons bei fehlendem Speaker/Abstract, Zähler „3 Sessions unvollständig“. Button „Agenda-Vorschau“ öffnet das öffentliche Raster des Tages (Komponente wiederverwenden). Klick auf Zeile öffnet Bearbeiten.
- **Dateien:** `Pages/Admin/AdminConference.razor`.

#### 17. Formulare: Fehler am Feld, ungespeicherte Änderungen, Komfort – Mittel, M
- **Problem:** `ApiClient.EnsureSuccessAsync` verkettet alle `Errors` zu einem String; `FormDialog.razor` zeigt ihn als Alert oben, nicht am betroffenen Feld. Schließen per Escape/X verwirft Eingaben ohne Rückfrage (`CloseOnEscapeKey="true"`, `CloseButton = true`). Slug muss von Hand getippt werden (`ConferenceDialog.razor`), Foto-URL ohne Vorschau und Links als Freitext pro Zeile (`SpeakerDialog.razor`). Snackbar sagt nur „Gespeichert.“.
- **Lösung:** `ApiException` um `Errors`-Dictionary erweitern; `FormDialog` überträgt sie per `ValidationMessageStore` an die Felder. Dirty-Check: beim Schließen mit Änderungen nachfragen. Slug aus Name vorschlagen, solange nicht manuell geändert. Avatar-Vorschau neben Foto-URL, Links als Liste mit Hinzufügen/Entfernen. Snackbar mit Objektnamen („Session „…“ gespeichert“) und Link „Ansehen“.
- **Dateien:** `Services/ApiClient.cs`, `Components/FormDialog.razor`, `Components/DialogExtensions.cs`, `Components/Dialogs/*.razor`, `Pages/Admin/*.razor`.

#### 20. Admin-Löschaktionen absichern – Niedrig, S
- **Problem:** Raum löschen scheitert erst nach Bestätigung mit 409 („Der Raum ist noch Sessions zugeordnet.“, `TrackRoomEndpoints.cs`) als Snackbar. Konferenz löschen entfernt alles inkl. Sessions mit einem Klick auf „Löschen“. Icon-Buttons in `AdminConferences.razor` haben nur Tooltips, kein `aria-label`.
- **Lösung:** Löschen von Räumen mit Sessions deaktivieren, Tooltip „4 Sessions zugeordnet“. Bestätigungsdialog nennt die Folgen mit Zahlen („12 Sessions, 3 Tracks, 4 Räume“); bei Konferenzen Eingabe des Namens zur Bestätigung. `aria-label` an allen Icon-Buttons.
- **Dateien:** `Pages/Admin/AdminConference.razor`, `Pages/Admin/AdminConferences.razor`, `Components/ConfirmDialog.razor`, `Components/DialogExtensions.cs`.

### Entscheidungen

- Phase 1 zuerst, weil Layout, `PageHeader` und `PageState` von fast allen späteren Punkten genutzt werden.
- Mobile Liste und Lückenlogik teilen sich eine Zeitachsen-Berechnung (`AgendaTimeline`), damit Raster und Liste gleiche Pausen zeigen.
- Alles, was clientseitig aus vorhandenen DTOs geht, bleibt clientseitig. API-Änderungen nur bei Punkt 10 (iCal-Endpunkt) und optional Punkt 12/15.

## 3. Nicht im Umfang

- Anmeldung, Rollen, Schutz von `/admin` (das Admin-Layout trennt nur optisch)
- Call for Papers, Registrierung, Ticketing
- Persönliche Agenda, Favoriten, Benachrichtigungen
- Drag-and-drop-Planung im Raster
- Automatisierte Tests (manuelle Prüfung wie im MVP)

## 4. Technologie, Architektur und Datenmodell

- Keine neuen Pakete. MudBlazor 9.14 deckt Skeleton, Breakpoints, Dark Mode und Dialoge ab; JS-Interop nur für den `.ics`-Download und Web Share.
- Neue Client-Bausteine: `PublicLayout`, `AdminLayout`, `PageHeader`, `AgendaList`, `AgendaTimeline`, `SessionQuickView`, `NowNext`, `AgendaCache`, `ColorContrast`, `IcsBuilder`, `ScheduleConflicts`.
- API: neu `GET /api/conferences/{slug}/agenda.ics` (`text/calendar`). Optional `SessionCount` in `ConferenceDto` und Speaker-Konflikt als Warnung beim Speichern.
- Datenmodell: unverändert. Echte Pausen-Einträge siehe Spätere Ausbaustufen.
- URL-Schema Agenda: `/konferenzen/{slug}?tag=2026-05-26&track=web&q=blazor&ansicht=liste`.

## 5. Umsetzungsschritte

### Phase 1: Grundlagen
- [ ] `PublicLayout` und `AdminLayout` anlegen, Admin-Seiten umhängen, Admin-Link aus öffentlicher Navigation entfernen (5)
- [ ] Skip-Link, `<main>`-Ziel, `h1` über neue `PageHeader`-Komponente, aktuellen Breadcrumb als Text (4, 18)
- [ ] Typografie und Abstände im Theme festlegen, Inline-Styles in CSS überführen, `Format.DayShort` (18)
- [ ] `PageState` mit Skeleton, Retry und Leerzuständen; deutsche 404-Seite; App-Shell in `index.html`; Roboto lokal (14)
- [ ] Track-Farben nur als Fläche, `ColorContrast`, Warnung im Track-Dialog, Seed-Farben anpassen (8)
- [ ] Prüfen: Tastatur (1× Tab bis Inhalt), Screenreader-Überschriften, 390 px und 1440 px

### Phase 2: Agenda
- [ ] `AgendaTimeline` mit Lückenerkennung ≥ 30 min; Raster auf Segmente umstellen, Pausen beschriften (2)
- [ ] Zeitspalte sticky, Zeilenhöhe erhöhen, Line-Clamp, Fokusstil (7)
- [ ] `AgendaList` für < 768 px inkl. Pausen (1)
- [ ] Query-Parameter für Tag, Track, Suche, Ansicht; trackfreie Sessions „Für alle“; `aria-pressed` (3)
- [ ] Suche über alle Tage mit Trefferzahl an den Tagesbuttons (9)
- [ ] Starttag „heute“, Jetzt-Linie, Leiste „Läuft gerade / Als Nächstes“ (6)
- [ ] `SessionQuickView` mit Abstract (7)
- [ ] Prüfen mit Playwright: Mobil keine horizontale Seitenscrollbar, Lücke 11:15–13:00 ≤ 40 px, Filter überlebt Reload

### Phase 3: Details und Entdecken
- [ ] `AgendaCache` registrieren und in Agenda/Session-Detail nutzen (11)
- [ ] Session-Detail: Rückweg mit Filtern, Parallel/Danach, Teilen (11)
- [ ] `IcsBuilder` + Download; API-Endpunkt `agenda.ics` (10)
- [ ] Startseite mit Status-Abschnitten und klickbaren Karten (12)
- [ ] Speaker-Übersicht mit Suche und Konferenzfilter; Detail-Links aufbereiten (13)
- [ ] `PaletteDark`, Systemerkennung, Umschalter, feste Farben ersetzen (19)

### Phase 4: Admin-Werkzeuge
- [ ] `ApiException.Errors` und Feldfehler im `FormDialog`; Dirty-Check beim Schließen (17)
- [ ] Slug-Vorschlag, Foto-Vorschau, Links-Liste, Snackbar mit Namen (17)
- [ ] `ScheduleConflicts`: Raum- und Speaker-Konflikte live im Session-Dialog, Raumbelegung, Dauer-Chips, Vorbelegung (15)
- [ ] Sessionliste gruppiert, filterbar, Hinweise auf Unvollständiges (16)
- [ ] Löschaktionen mit Folgen-Anzeige, Raum-Löschen sperren, `aria-label` an Icon-Buttons (20)
- [ ] Manuell durchklicken: Session mit Konflikt anlegen, Dialog mit Änderungen schließen, Raum mit Sessions löschen
- [ ] README (Abschnitt Bedienung) aktualisieren und Plan-Checkliste abhaken

## 6. Annahmen

- „Jetzt“ und „Heute“ nutzen die Uhr des Browsers. Das passt, solange Besucher in der Zeitzone der Konferenz sind (MVP-Annahme: alle Zeiten lokal). Die `.ics`-Dateien schreiben Zeiten ohne Zeitzone („floating“).
- Breakpoint für die Listenansicht ist 768 px (MudBlazor `md` = 960 px wäre für Tablets im Querformat zu früh).
- Eine Lücke gilt ab 30 Minuten ohne Session in allen Räumen als Pause.
- Die Daten einer Konferenz (Agenda) bleiben klein genug, um Suche und Konfliktprüfung im Client zu machen (Größenordnung bis einige hundert Sessions).
- Track-Farben werden weiterhin frei gewählt; der Plan warnt nur, er erzwingt keinen Kontrast.

## 7. Spätere Ausbaustufen

- Pausen, Mittagessen und Keynote-Blöcke als eigene Einträge im Datenmodell (`Session.Kind` oder Entität `Break`), auch raumübergreifend
- Persönliche Agenda mit Favoriten und eigenem iCal-Abo (setzt Anmeldung oder Gerätespeicher-Konzept voraus)
- Drag-and-drop-Planung im Admin-Raster
- Zeitzonen pro Konferenz
- PWA/Offline-Modus für die Agenda vor Ort; Prerendering für schnelleren ersten Aufruf und Teilen-Vorschauen (Open Graph)
- Mehrsprachigkeit (Deutsch/Englisch)
- Schutz des Admin-Bereichs durch Anmeldung und Rollen
