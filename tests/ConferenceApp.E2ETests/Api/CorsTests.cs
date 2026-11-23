using System.Net;
using ConferenceApp.E2ETests.Infrastructure;

namespace ConferenceApp.E2ETests.Api;

public class CorsTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preflight_vom_Client_Origin_wird_erlaubt()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "api/conferences");
        request.Headers.Add("Origin", Fixture.Client.Origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await Http.SendAsync(request, Ct);

        Assert.True(response.IsSuccessStatusCode, $"Status {(int)response.StatusCode}");
        Assert.Equal(Fixture.Client.Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.Contains("content-type", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_vom_Client_Origin_liefert_CORS_Header()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/conferences");
        request.Headers.Add("Origin", Fixture.Client.Origin);

        var response = await Http.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Fixture.Client.Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Fremder_Origin_erhaelt_keine_CORS_Freigabe()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/conferences");
        request.Headers.Add("Origin", "http://evil.example");

        var response = await Http.SendAsync(request, Ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Fehlerantworten_tragen_ebenfalls_CORS_Header()
    {
        // Sonst kann der Client die ProblemDetails (z. B. Validierungsfehler) im Browser nicht lesen.
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/conferences/999999");
        request.Headers.Add("Origin", Fixture.Client.Origin);

        var response = await Http.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Fixture.Client.Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
