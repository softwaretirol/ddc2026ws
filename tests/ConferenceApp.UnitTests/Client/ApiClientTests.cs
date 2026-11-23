using System.Net;
using System.Text;
using ConferenceApp.Client.Services;
using ConferenceApp.Shared;

namespace ConferenceApp.UnitTests.Client;

public class ApiClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static (ApiClient Client, FakeHandler Handler) Create(HttpStatusCode status, string? body, string mediaType = "application/problem+json")
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(status)
        {
            Content = body is null ? new ByteArrayContent([]) : new StringContent(body, Encoding.UTF8, mediaType),
        });
        return (new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }), handler);
    }

    [Fact]
    public async Task Erfolg_liefert_deserialisierte_Daten_und_richtige_URL()
    {
        var (client, handler) = Create(HttpStatusCode.OK,
            """[{"id":1,"name":"DDC","slug":"ddc","description":null,"startDate":"2026-12-01","endDate":"2026-12-02","location":"Innsbruck"}]""",
            "application/json");

        var list = await client.GetConferencesAsync();

        var c = Assert.Single(list);
        Assert.Equal("ddc", c.Slug);
        Assert.Equal(new DateOnly(2026, 12, 2), c.EndDate);
        Assert.Equal("http://api.test/api/conferences", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Slug_wird_in_der_URL_kodiert()
    {
        var (client, handler) = Create(HttpStatusCode.NotFound, null);
        await Assert.ThrowsAsync<ApiException>(() => client.GetAgendaAsync("a b"));
        Assert.Equal("/api/conferences/a%20b/agenda", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Validierungsfehler_werden_zu_einer_Meldung_zusammengefasst()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","status":400,"errors":{"Slug":["Dieser Slug ist bereits vergeben."],"Name":["Name ist erforderlich."]}}""");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.CreateConferenceAsync(new ConferenceRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains("Dieser Slug ist bereits vergeben.", ex.Message);
        Assert.Contains("Name ist erforderlich.", ex.Message);
        Assert.DoesNotContain("One or more", ex.Message);
    }

    [Fact]
    public async Task Detail_hat_Vorrang_vor_Titel()
    {
        var (client, _) = Create(HttpStatusCode.Conflict,
            """{"title":"Conflict","status":409,"detail":"Der Raum ist noch Sessions zugeordnet."}""");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.DeleteRoomAsync(1, 2));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.Equal("Der Raum ist noch Sessions zugeordnet.", ex.Message);
        Assert.False(ex.IsNotFound);
    }

    [Fact]
    public async Task NotFound_ohne_Detail_liefert_deutsche_Meldung()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetConferenceAsync(4711));

        Assert.True(ex.IsNotFound);
        Assert.Equal("Nicht gefunden.", ex.Message);
    }

    [Fact]
    public async Task Titel_wenn_weder_Fehler_noch_Detail()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, """{"title":"An error occurred while processing your request.","status":500}""");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetSpeakersAsync());
        Assert.Equal("An error occurred while processing your request.", ex.Message);
    }

    [Fact]
    public async Task Kein_ProblemDetails_Body_liefert_Statuscode()
    {
        var (client, _) = Create(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>", "text/html");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetSpeakersAsync());
        Assert.Equal(HttpStatusCode.BadGateway, ex.Status);
        Assert.Equal("Fehler 502.", ex.Message);
    }

    [Fact]
    public async Task Leerer_Body_bei_404_liefert_Nicht_gefunden()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.DeleteSpeakerAsync(1));
        Assert.Equal("Nicht gefunden.", ex.Message);
    }

    [Fact]
    public async Task Nicht_erreichbare_API_liefert_ServiceUnavailable()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetConferencesAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.Status);
        Assert.Equal("Die API ist nicht erreichbar.", ex.Message);
    }

    [Fact]
    public async Task Schreibaufrufe_verwenden_die_richtigen_Methoden_und_Pfade()
    {
        var handler = new FakeHandler(r => new HttpResponseMessage(r.Method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":5,"conferenceId":3,"name":"Saal","capacity":10}""", Encoding.UTF8, "application/json"),
        });
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

        await client.CreateRoomAsync(3, new RoomRequest { Name = "Saal" });
        await client.UpdateRoomAsync(3, 5, new RoomRequest { Name = "Saal" });
        await client.DeleteRoomAsync(3, 5);

        Assert.Collection(handler.Requests,
            r => { Assert.Equal(HttpMethod.Post, r.Method); Assert.Equal("/api/conferences/3/rooms", r.RequestUri!.AbsolutePath); },
            r => { Assert.Equal(HttpMethod.Put, r.Method); Assert.Equal("/api/conferences/3/rooms/5", r.RequestUri!.AbsolutePath); },
            r => { Assert.Equal(HttpMethod.Delete, r.Method); Assert.Equal("/api/conferences/3/rooms/5", r.RequestUri!.AbsolutePath); });
    }
}
