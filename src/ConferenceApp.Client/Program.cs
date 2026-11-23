using ConferenceApp.Client;
using ConferenceApp.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Adresse der API aus wwwroot/appsettings.json (Aspire startet die API auf diesem Port).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5198/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AgendaCache>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
