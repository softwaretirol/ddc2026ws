using System.Net;
using ConferenceApp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ConferenceApp.E2ETests.Infrastructure.E2EFixture))]
[assembly: Xunit.v3.Parallelization(MaxThreads = 4)]

namespace ConferenceApp.E2ETests.Infrastructure;

/// <summary>
/// Ein Postgres-Container, eine API (echtes Kestrel-HTTP) und ein statischer Host für den WASM-Client pro Testlauf.
/// Tests isolieren ihre Daten über eigene Konferenzen mit eindeutigem Slug; die Seed-Konferenz wird nur gelesen.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    public const string SeedSlug = "devconf-2026";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private WebApplicationFactory<Program>? _api;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _browserLock = new(1, 1);

    public ClientHost Client { get; private set; } = null!;

    /// <summary>Basisadresse der API inkl. abschließendem Schrägstrich.</summary>
    public Uri ApiBaseAddress { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Client = await ClientHost.StartAsync();

        _api = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:conferencedb", _postgres.GetConnectionString());
            b.UseSetting("ClientOrigin", Client.Origin);
            // Testausgabe übersichtlich halten (EF-Core-SQL etc. nur ab Warning).
            b.UseSetting("Logging:LogLevel:Default", "Warning");
            b.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Warning");
        });
        _api.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
        _api.StartServer();

        var address = _api.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        ApiBaseAddress = new Uri(address.TrimEnd('/') + "/");
    }

    /// <summary>HTTP-Client gegen die echte API (Kestrel), nicht gegen den TestServer.</summary>
    public HttpClient CreateApiClient() => new() { BaseAddress = ApiBaseAddress };

    /// <summary>Direkter DB-Zugriff zum Prüfen der Persistenz.</summary>
    public async Task<T> WithDbAsync<T>(Func<ConferenceDbContext, Task<T>> query)
    {
        await using var scope = _api!.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ConferenceDbContext>());
    }

    public async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null) return _browser;
        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null) return _browser;

            await Client.EnsurePublishedAsync();

            // Installiert Chromium, falls nicht vorhanden (idempotent; entspricht "playwright.ps1 install chromium").
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (exitCode != 0)
                throw new InvalidOperationException($"Playwright konnte Chromium nicht installieren (Exitcode {exitCode}).");

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        if (_api is not null) await _api.DisposeAsync();
        if (Client is not null) await Client.DisposeAsync();
        await _postgres.DisposeAsync();
        _browserLock.Dispose();
    }
}
