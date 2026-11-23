using System.Text.Json;
using Microsoft.Playwright;

namespace ConferenceApp.E2ETests.Infrastructure;

/// <summary>
/// Basis für UI-Tests: eigener Browser-Kontext pro Test, appsettings.json per Route auf die Test-API umgebogen,
/// Trace und Screenshot bei Fehlschlag unter TestResults/playwright im Ausgabeordner.
/// </summary>
public abstract class UiTestBase(E2EFixture fixture) : ApiTestBase(fixture), IAsyncLifetime
{
    private IBrowserContext? _context;

    protected IPage Page { get; private set; } = null!;

    protected static readonly string ArtifactsDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "playwright");

    public async ValueTask InitializeAsync()
    {
        var browser = await Fixture.GetBrowserAsync();
        _context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Fixture.Client.Origin,
            Locale = "de-DE",
            ViewportSize = new ViewportSize { Width = 1440, Height = 1000 },
        });
        _context.SetDefaultTimeout(15_000);
        await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = false });

        // Der Client liest die API-Adresse aus wwwroot/appsettings.json – hier auf die Test-API zeigen lassen.
        var settings = JsonSerializer.Serialize(new { ApiBaseUrl = Fixture.ApiBaseAddress.ToString() });
        await _context.RouteAsync("**/appsettings*.json", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = route.Request.Url.Contains("appsettings.json", StringComparison.OrdinalIgnoreCase) ? settings : "{}",
        }));

        Page = await _context.NewPageAsync();
        Assertions.SetDefaultExpectTimeout(15_000);
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is null) return;

        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        if (failed)
        {
            Directory.CreateDirectory(ArtifactsDir);
            var name = Sanitize(TestContext.Current.Test?.TestDisplayName ?? Guid.NewGuid().ToString("N"));
            try
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ArtifactsDir, name + ".png"), FullPage = true });
            }
            catch (PlaywrightException) { /* Seite evtl. bereits geschlossen */ }
            await _context.Tracing.StopAsync(new TracingStopOptions { Path = Path.Combine(ArtifactsDir, name + ".trace.zip") });
            TestContext.Current.TestOutputHelper?.WriteLine($"Playwright-Artefakte: {Path.Combine(ArtifactsDir, name)}.png / .trace.zip");
        }
        else
        {
            await _context.Tracing.StopAsync();
        }

        await _context.DisposeAsync();
        Dispose();
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return clean.Length > 120 ? clean[..120] : clean;
    }

    /// <summary>Navigiert zu einer Client-Route und wartet, bis Blazor gerendert hat (Ladeanzeige verschwunden).</summary>
    protected async Task GotoAsync(string path)
    {
        await Page.GotoAsync(path);
        // Die App-Leiste wird vom Layout gerendert, sobald die WASM-App läuft.
        await Assertions.Expect(Page.GetByRole(AriaRole.Banner).First).ToBeVisibleAsync(new() { Timeout = 60_000 });
    }

    protected ILocator Dialog(string title) =>
        Page.GetByRole(AriaRole.Dialog).Filter(new LocatorFilterOptions { HasText = title });
}
