using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.E2ETests.Infrastructure;

/// <summary>
/// Kleiner statischer Kestrel-Host für den veröffentlichten Blazor-WASM-Client (wwwroot + _framework)
/// mit SPA-Fallback auf index.html. Der Host startet sofort (damit der Origin für CORS feststeht);
/// der Client wird erst beim ersten UI-Test veröffentlicht.
/// </summary>
public sealed class ClientHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private bool _published;

    private ClientHost(WebApplication app, string root, string origin)
    {
        _app = app;
        Root = root;
        Origin = origin;
    }

    /// <summary>Verzeichnis mit index.html und _framework.</summary>
    public string Root { get; }

    /// <summary>z. B. http://127.0.0.1:53123 (ohne abschließenden Schrägstrich).</summary>
    public string Origin { get; }

    public static async Task<ClientHost> StartAsync()
    {
        // Ein bereits veröffentlichter Client kann per Umgebungsvariable vorgegeben werden (z. B. in CI).
        var preset = Environment.GetEnvironmentVariable("E2E_CLIENT_WWWROOT");
        var root = !string.IsNullOrWhiteSpace(preset)
            ? Path.GetFullPath(preset)
            : Path.Combine(AppContext.BaseDirectory, "client-publish", "wwwroot");
        Directory.CreateDirectory(root);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));

        var app = builder.Build();

        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".wasm"] = "application/wasm";
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".blat"] = "application/octet-stream";
        contentTypes.Mappings[".webcil"] = "application/octet-stream";
        contentTypes.Mappings[".pdb"] = "application/octet-stream";
        contentTypes.Mappings[".json"] = "application/json";
        contentTypes.Mappings[".js"] = "text/javascript";
        contentTypes.Mappings[".mjs"] = "text/javascript";

        var files = new PhysicalFileProvider(root);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });
        // SPA-Fallback: Deep-Links wie /konferenzen/abc liefern index.html.
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files, ContentTypeProvider = contentTypes });

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new ClientHost(app, root, address.TrimEnd('/'));
    }

    /// <summary>Veröffentlicht den Client einmal pro Testlauf (dotnet publish), sofern nicht vorgegeben.</summary>
    public async Task EnsurePublishedAsync()
    {
        if (_published) return;
        await _publishLock.WaitAsync();
        try
        {
            if (_published) return;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_CLIENT_WWWROOT")))
                await PublishAsync(Path.GetDirectoryName(Root)!);

            if (!File.Exists(Path.Combine(Root, "index.html")) || !Directory.Exists(Path.Combine(Root, "_framework")))
                throw new InvalidOperationException($"Im Verzeichnis '{Root}' fehlt der veröffentlichte Client (index.html, _framework).");
            _published = true;
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private static async Task PublishAsync(string outputDir)
    {
        var project = Path.Combine(RepoPaths.Root, "src", "ConferenceApp.Client", "ConferenceApp.Client.csproj");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoPaths.Root,
        };
        foreach (var arg in new[] { "publish", project, "-c", "Release", "-o", outputDir, "-nologo", "-v", "quiet" })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("dotnet publish konnte nicht gestartet werden.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(10));
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"dotnet publish des Clients schlug fehl ({process.ExitCode}):\n{await stdout}\n{await stderr}");
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _publishLock.Dispose();
    }
}

internal static class RepoPaths
{
    /// <summary>Repository-Wurzel (Verzeichnis mit ConferenceApp.slnx).</summary>
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ConferenceApp.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("ConferenceApp.slnx nicht gefunden.");
    }
}
