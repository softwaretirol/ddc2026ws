using ConferenceApp.Api.Data;
using ConferenceApp.Api.Endpoints;
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

// Migrationen anwenden und Demodaten einspielen.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConferenceDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

app.Run();
