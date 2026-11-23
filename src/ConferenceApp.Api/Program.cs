using ConferenceApp.Api.Data;
using ConferenceApp.Api.Endpoints;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Connection-String "conferencedb" wird von Aspire (WithReference) injiziert.
builder.Services.AddDbContext<ConferenceDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("conferencedb")
        ?? throw new InvalidOperationException("Connection-String 'conferencedb' fehlt. Bitte über den AppHost starten.")));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Der WASM-Client laeuft auf einem eigenen Origin; Aspire uebergibt ihn als "ClientOrigin".
var clientOrigins = (builder.Configuration["ClientOrigin"] ?? "http://localhost:5226")
    .Split(';', StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(clientOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapConferenceEndpoints();
app.MapTrackEndpoints();
app.MapRoomEndpoints();
app.MapSessionEndpoints();
app.MapSpeakerEndpoints();

// Im Container liegt der veröffentlichte Blazor-Client in wwwroot: dann liefert die API ihn selbst aus
// (gleicher Origin, kein CORS nötig). In der Entwicklung mit Aspire gibt es kein wwwroot – dort entfällt das.
if (app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists)
{
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".wasm"] = "application/wasm";
    contentTypes.Mappings[".dat"] = "application/octet-stream";
    contentTypes.Mappings[".blat"] = "application/octet-stream";
    contentTypes.Mappings[".webcil"] = "application/octet-stream";
    contentTypes.Mappings[".pdb"] = "application/octet-stream";

    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
    // Unbekannte API-Routen bleiben 404 statt index.html; alles andere sind Client-Routen.
    app.MapFallback("/api/{**rest}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

// Migrationen anwenden und Demodaten einspielen.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConferenceDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

app.Run();
