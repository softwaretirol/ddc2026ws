using ConferenceApp.Client;
using ConferenceApp.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Adresse der API aus wwwroot/appsettings.json. In der Entwicklung (Aspire) läuft die API auf eigenem Port.
// Leer oder nicht gesetzt (z. B. im Container, wo die API den Client selbst ausliefert): gleicher Origin.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var apiBase = string.IsNullOrWhiteSpace(apiBaseUrl)
    ? new Uri(builder.HostEnvironment.BaseAddress)
    : new Uri(apiBaseUrl);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBase });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<ThemeService>();
// Uhr für "Heute/Jetzt" – injizierbar, damit sich eine feste Zeit vorgeben lässt (Tests/Vorschau). Nur hier registrieren.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AgendaCache>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
