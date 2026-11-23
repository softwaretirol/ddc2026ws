using System.Text.RegularExpressions;
using ConferenceApp.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ConferenceApp.E2ETests.Ui;

/// <summary>Öffentliche Seiten anhand der (nur gelesenen) Seed-Konferenz „Dev Conference 2026“ (01.–02.12.2026).</summary>
public class PublicUiTests(E2EFixture fixture) : UiTestBase(fixture)
{
    private const string AgendaPath = "/konferenzen/" + E2EFixture.SeedSlug;

    // Agenda mit festem Tag aufrufen (#6: sonst wäre der Starttag an einem Konferenztag „heute“).
    private const string AgendaDay1 = AgendaPath + "?tag=2026-12-01";

    // Raster (Desktop) und Liste (mobil) sind beide im DOM; nur die sichtbare Darstellung zählt.
    private ILocator SessionLink(string title) =>
        Page.GetByRole(AriaRole.Link, new() { Name = title }).Filter(new() { Visible = true });

    [Fact]
    public async Task Startseite_listet_Seed_Konferenz_und_fuehrt_zur_Agenda()
    {
        await GotoAsync("/");

        await Expect(Page.GetByText("Dev Conference 2026").First).ToBeVisibleAsync();
        // Link über das Routenziel – unabhängig von der Gestaltung der Konferenzkarte.
        await Page.Locator($"a[href$='{AgendaPath}']").First.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(AgendaPath) + "$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Dev Conference 2026" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Agenda_zeigt_Sessions_des_ersten_Tages_und_wechselt_den_Tag()
    {
        await GotoAsync(AgendaDay1);

        // Tag 1
        await Expect(SessionLink("Was ist neu in .NET 11")).ToBeVisibleAsync();
        await Expect(SessionLink("Blazor WebAssembly in der Praxis")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Saal Tirol").Filter(new() { Visible = true }).First).ToBeVisibleAsync();
        await Expect(SessionLink("Aspire: Lokale Orchestrierung")).ToHaveCountAsync(0);

        // Tag 2
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex(@"02\.12\.") }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("tag=2026-12-02"));   // #3: Tag steht in der URL

        await Expect(SessionLink("Aspire: Lokale Orchestrierung")).ToBeVisibleAsync();
        await Expect(SessionLink("Closing Panel")).ToBeVisibleAsync();
        await Expect(SessionLink("Was ist neu in .NET 11")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Track_Filter_zeigt_nur_Sessions_des_Tracks()
    {
        await GotoAsync(AgendaDay1);
        await Expect(SessionLink("Was ist neu in .NET 11")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Web & Frontend", Exact = true }).ClickAsync();

        await Expect(SessionLink("Blazor WebAssembly in der Praxis")).ToBeVisibleAsync();
        await Expect(SessionLink("Barrierefreie Web-UIs")).ToBeVisibleAsync();
        await Expect(SessionLink("Was ist neu in .NET 11")).ToHaveCountAsync(0);
        await Expect(SessionLink("Postgres für .NET-Entwickler")).ToHaveCountAsync(0);

        // Filter zurücksetzen
        await Page.GetByRole(AriaRole.Button, new() { Name = "Alle Tracks" }).ClickAsync();
        await Expect(SessionLink("Was ist neu in .NET 11")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Session_Detail_zeigt_Zeit_Raum_und_Speaker()
    {
        await GotoAsync(AgendaDay1);

        // Klick auf die Karte öffnet die Quick-View, von dort geht es zur Detailseite.
        await SessionLink("Minimal APIs richtig strukturieren").ClickAsync();
        var quickView = Page.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Minimal APIs richtig strukturieren" });
        await Expect(quickView).ToContainTextAsync("13:00–13:45");
        await Expect(quickView).ToContainTextAsync("Saal Tirol");
        await quickView.GetByRole(AriaRole.Link, new() { Name = "Zur Detailseite" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(AgendaPath) + @"/sessions/\d+"));
        var main = Page.GetByRole(AriaRole.Main);
        await Expect(main.GetByRole(AriaRole.Heading, new() { Name = "Minimal APIs richtig strukturieren", Level = 1 })).ToBeVisibleAsync();
        await Expect(main.GetByText("13:00–13:45").First).ToBeVisibleAsync();
        await Expect(main.GetByText("Saal Tirol").First).ToBeVisibleAsync();
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = "Anna Berger" }).First).ToBeVisibleAsync();
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = "David Novak" }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Unbekannte_Konferenz_zeigt_Hinweis_statt_Agenda()
    {
        await GotoAsync("/konferenzen/gibt-es-nicht");
        await Expect(Page.GetByText("Diese Konferenz gibt es nicht.")).ToBeVisibleAsync();
    }
}
