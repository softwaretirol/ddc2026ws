using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ConferenceApp.E2ETests.Infrastructure;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ConferenceApp.E2ETests.Ui;

/// <summary>Admin-Abläufe. Jeder Test arbeitet auf einer eigenen Konferenz (eindeutiger Slug).</summary>
public class AdminUiTests(E2EFixture fixture) : UiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Eingabefeld im Dialog über sein Label (Pflichtfelder tragen ein „*“).</summary>
    private static ILocator Field(ILocator dialog, string label) =>
        dialog.GetByLabel(new Regex("^" + Regex.Escape(label) + @"\*?$"));

    private async Task OpenTabAsync(string name) =>
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^" + Regex.Escape(name)) }).ClickAsync();

    private async Task SelectAsync(ILocator dialog, string label, string option)
    {
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = label }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = option }).ClickAsync();
    }

    [Fact]
    public async Task Konferenz_anlegen_mit_Validierungsfehlern_im_Dialog()
    {
        var slug = UniqueSlug("ui");
        var name = "UI-Konferenz " + slug[^6..];

        await GotoAsync("/admin");
        await Button("Neue Konferenz").ClickAsync();
        var dialog = Dialog("Neue Konferenz");
        await Expect(dialog).ToBeVisibleAsync();

        // 1) Pflichtfelder leer -> Validierungsfehler am Feld, Dialog bleibt offen
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(dialog.GetByText("Name ist erforderlich.")).ToBeVisibleAsync();
        await Expect(dialog).ToBeVisibleAsync();

        // 2) Ungültiges Slug-Format
        await Field(dialog, "Name").FillAsync(name);
        await Field(dialog, "Slug (für die URL)").FillAsync("Mit Leerzeichen");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(dialog.GetByText("Nur Kleinbuchstaben, Ziffern und Bindestriche.")).ToBeVisibleAsync();

        // 3) Slug bereits vergeben -> Fehler der API im Dialog, Dialog bleibt offen
        await Field(dialog, "Slug (für die URL)").FillAsync(E2EFixture.SeedSlug);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(dialog.GetByText("Dieser Slug ist bereits vergeben.")).ToBeVisibleAsync();

        // 4) Gültig -> gespeichert, Dialog zu, Zeile in der Tabelle
        await Field(dialog, "Slug (für die URL)").FillAsync(slug);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Row).Filter(new() { HasText = slug })).ToBeVisibleAsync();

        var agenda = await GetAsync<AgendaDto>($"api/conferences/{slug}/agenda");
        Assert.Equal(name, agenda.Conference.Name);
    }

    // #17: Slug aus dem Namen vorschlagen, solange nicht manuell geändert; Ergebnis passt zum Slug-Format der API.
    [Fact]
    public async Task Slug_wird_aus_dem_Namen_vorgeschlagen_inklusive_Umlauten()
    {
        await GotoAsync("/admin");
        await Button("Neue Konferenz").ClickAsync();
        var dialog = Dialog("Neue Konferenz");
        var slug = Field(dialog, "Slug (für die URL)");

        await Field(dialog, "Name").FillAsync("Größte Österreich-Tage: Über .NET 2027");
        await Field(dialog, "Name").PressAsync("Tab");
        await Expect(slug).ToHaveValueAsync("groesste-oesterreich-tage-ueber-net-2027");

        // Manuell geänderter Slug wird nicht mehr überschrieben
        await slug.FillAsync("mein-slug");
        await slug.PressAsync("Tab");
        await Field(dialog, "Name").FillAsync("Ganz anderer Name");
        await Field(dialog, "Name").PressAsync("Tab");
        await Expect(slug).ToHaveValueAsync("mein-slug");
    }

    [Fact]
    public async Task Raum_Track_und_Session_anlegen()
    {
        var conf = await CreateConferenceAsync();
        var speaker = await CreateSpeakerAsync("UI Speaker " + Guid.NewGuid().ToString("N")[..6]);

        await GotoAsync($"/admin/konferenzen/{conf.Id}");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = conf.Name })).ToBeVisibleAsync();

        // Raum
        await OpenTabAsync("Räume");
        await Button("Neuer Raum").ClickAsync();
        var roomDialog = Dialog("Neuer Raum");
        await Field(roomDialog, "Name").FillAsync("Saal Innsbruck");
        await Field(roomDialog, "Kapazität (optional)").FillAsync("150");
        await roomDialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(roomDialog).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = "Saal Innsbruck" })).ToBeVisibleAsync();

        // Track
        await OpenTabAsync("Tracks");
        await Button("Neuer Track").ClickAsync();
        var trackDialog = Dialog("Neuer Track");
        await Field(trackDialog, "Name").FillAsync("Security");
        await Field(trackDialog, "Farbe (#RRGGBB)").FillAsync("#C62828");
        await trackDialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(trackDialog).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = "Security" })).ToBeVisibleAsync();

        // Session
        await OpenTabAsync("Sessions");
        await Button("Neue Session").ClickAsync();
        var sessionDialog = Dialog("Neue Session");
        await Field(sessionDialog, "Titel").FillAsync("Sichere Web-APIs");
        await SelectAsync(sessionDialog, "Track", "Security");
        await SelectAsync(sessionDialog, "Speaker", speaker.Name);
        await Page.Keyboard.PressAsync("Escape"); // Mehrfachauswahl schließen
        await sessionDialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(sessionDialog).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Sichere Web-APIs" })).ToBeVisibleAsync();

        // Persistenz über die API prüfen
        var session = Assert.Single(await GetAsync<List<SessionDto>>($"api/conferences/{conf.Id}/sessions"));
        Assert.Equal(("Sichere Web-APIs", conf.StartDate, "Saal Innsbruck", "Security"), (session.Title, session.Day, session.RoomName, session.TrackName));
        Assert.Equal([speaker.Id], session.Speakers.Select(s => s.Id));
        var room = Assert.Single(await GetAsync<List<RoomDto>>($"api/conferences/{conf.Id}/rooms"));
        Assert.Equal(150, room.Capacity);
    }

    [Fact]
    public async Task Speaker_anlegen()
    {
        var name = "Neue Speakerin " + Guid.NewGuid().ToString("N")[..6];

        await GotoAsync("/admin/speaker");
        await Button("Neuer Speaker").ClickAsync();
        var dialog = Dialog("Neuer Speaker");
        await Field(dialog, "Name").FillAsync(name);
        await Field(dialog, "Firma").FillAsync("Contoso");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Row).Filter(new() { HasText = name })).ToBeVisibleAsync();
        var speakers = await GetAsync<List<SpeakerDto>>("api/speakers");
        Assert.Equal("Contoso", Assert.Single(speakers, s => s.Name == name).Company);
    }

    [Fact]
    public async Task Foto_URL_eines_Speakers_wieder_entfernen()
    {
        var speaker = await CreateSpeakerAsync("Foto Speaker " + Guid.NewGuid().ToString("N")[..6]);
        var update = await Http.PutAsJsonAsync($"api/speakers/{speaker.Id}", new SpeakerRequest { Name = speaker.Name, PhotoUrl = "https://example.com/foto.png" }, Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await GotoAsync("/admin/speaker");
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = speaker.Name });
        await row.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten" }).ClickAsync();
        var dialog = Dialog("Speaker bearbeiten");
        await Field(dialog, "Foto-URL").FillAsync("");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();
        var reloaded = await GetAsync<SpeakerDetailDto>($"api/speakers/{speaker.Id}");
        Assert.Null(reloaded.Speaker.PhotoUrl);
    }

    [Fact]
    public async Task Raumkonflikt_wird_im_Session_Dialog_angezeigt()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id, "Konfliktraum");
        await CreateSessionAsync(conf.Id, Session(room.Id, conf.StartDate, 9, 30, 10, 30, title: "Bestehende Session"));

        await GotoAsync($"/admin/konferenzen/{conf.Id}");
        await Button("Neue Session").ClickAsync();
        var dialog = Dialog("Neue Session");
        await Field(dialog, "Titel").FillAsync("Kollidierende Session");
        // #15: Vorschlag ist frei (Beginn = Ende der letzten Session); eine überlappende Zeit wird live als „Raum belegt“ gemeldet.
        await Expect(Field(dialog, "Beginn")).ToHaveValueAsync("10:30");
        // Beginn 09:00 bei 60 min Dauer überschneidet sich mit 09:30–10:30.
        await Field(dialog, "Beginn").FillAsync("09:00");
        await Field(dialog, "Beginn").PressAsync("Tab");

        await Expect(dialog.GetByText("Raum belegt: „Bestehende Session“ (09:30–10:30)").First).ToBeVisibleAsync();

        // Mini-Zeitleiste: Stundenbeschriftungen aufsteigend und verschieden (früher stand überall dieselbe Uhrzeit).
        var hourLabels = await dialog.GetByText(new Regex(@"^\d{2}:00$")).AllInnerTextsAsync();
        Assert.True(hourLabels.Count >= 2, string.Join(" ", hourLabels));
        Assert.Equal(hourLabels.Order(StringComparer.Ordinal), hourLabels);
        Assert.Equal(hourLabels.Count, hourLabels.Distinct().Count());

        // Speichern ist blockiert, wie bei der API-Regel
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();

        var count = await Fixture.WithDbAsync(db => db.Sessions.CountAsync(s => s.ConferenceId == conf.Id, Ct));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Raum_loeschen_mit_Bestaetigung()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id, "Wegwerfraum");

        await GotoAsync($"/admin/konferenzen/{conf.Id}");
        await OpenTabAsync("Räume");
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Wegwerfraum" });

        // Abbrechen lässt den Raum bestehen
        await row.GetByRole(AriaRole.Button, new() { Name = "Löschen" }).ClickAsync();
        var confirm = Dialog("Löschen bestätigen");
        await Expect(confirm).ToContainTextAsync("Wegwerfraum");
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Abbrechen" }).ClickAsync();
        await Expect(confirm).ToBeHiddenAsync();
        await AssertStatusAsync(HttpStatusCode.OK, Http.GetAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct));

        // Bestätigen löscht
        await row.GetByRole(AriaRole.Button, new() { Name = "Löschen" }).ClickAsync();
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Löschen" }).ClickAsync();
        await Expect(row).ToHaveCountAsync(0);
        await AssertStatusAsync(HttpStatusCode.NotFound, Http.GetAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct));
    }

    [Fact]
    public async Task Raum_mit_Sessions_kann_nicht_geloescht_werden()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id, "Belegter Raum");
        await CreateSessionAsync(conf.Id, Session(room.Id, conf.StartDate, 9, 0, 10, 0));

        await GotoAsync($"/admin/konferenzen/{conf.Id}");
        await OpenTabAsync("Räume");
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Belegter Raum" });

        // #20: Die Folge wird vorab gezeigt – Löschen ist gesperrt und nennt den Grund (statt erst 409 nach dem Klick).
        var delete = row.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Löschen") });
        await Expect(delete).ToBeDisabledAsync();
        await Expect(delete).ToHaveAccessibleNameAsync(new Regex("1 Session"));
        await Expect(row).ToBeVisibleAsync();
        await AssertStatusAsync(HttpStatusCode.OK, Http.GetAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct));
    }

    [Fact]
    public async Task Konferenz_loeschen_mit_Bestaetigung()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        await CreateSessionAsync(conf.Id, Session(room.Id, conf.StartDate, 9, 0, 10, 0));

        await GotoAsync("/admin");
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = conf.Slug });
        await row.GetByRole(AriaRole.Button, new() { Name = "Löschen" }).ClickAsync();
        var confirm = Dialog("Löschen bestätigen");
        await Expect(confirm).ToContainTextAsync(conf.Name);

        // #20: Bestätigen erst nach Eingabe des Konferenznamens
        var confirmButton = confirm.GetByRole(AriaRole.Button, new() { Name = "Löschen" });
        await Expect(confirmButton).ToBeDisabledAsync();
        await confirm.GetByLabel("Name").FillAsync(conf.Name[..^1]);
        await Expect(confirmButton).ToBeDisabledAsync();
        await confirm.GetByLabel("Name").FillAsync(conf.Name);
        await confirmButton.ClickAsync();

        await Expect(row).ToHaveCountAsync(0);
        await AssertStatusAsync(HttpStatusCode.NotFound, Http.GetAsync($"api/conferences/{conf.Id}", Ct));
        var sessions = await Fixture.WithDbAsync(db => db.Sessions.CountAsync(s => s.ConferenceId == conf.Id, Ct));
        Assert.Equal(0, sessions);
    }
}
