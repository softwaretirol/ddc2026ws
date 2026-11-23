var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var db = postgres.AddDatabase("conferencedb");

var api = builder.AddDotnetProject("api", "../ConferenceApp.Api/ConferenceApp.Api.csproj", o => o.LaunchProfileName = "http")
    .WithReference(db)
    .WaitFor(db);

var client = builder.AddDotnetProject("client", "../ConferenceApp.Client/ConferenceApp.Client.csproj", o => o.LaunchProfileName = "http")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

// CORS: der API den Origin des Clients mitteilen.
api.WithEnvironment("ClientOrigin", client.GetEndpoint("http"));

builder.Build().Run();

