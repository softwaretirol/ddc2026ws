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

    // #17: Feldfehler stehen zusätzlich strukturiert zur Verfügung, damit der Dialog sie am Feld zeigt.
    [Fact]
    public async Task Validierungsfehler_stehen_pro_Feld_zur_Verfuegung()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest,
            """{"status":400,"errors":{"Slug":["Dieser Slug ist bereits vergeben."]}}""");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.CreateConferenceAsync(new ConferenceRequest()));

        Assert.Equal(["Dieser Slug ist bereits vergeben."], ex.Errors["Slug"]);
    }

    [Fact]
    public async Task Ohne_Validierungsfehler_ist_Errors_leer()
    {
        var (client, _) = Create(HttpStatusCode.Conflict, """{"status":409,"detail":"x"}""");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.DeleteRoomAsync(1, 2));
        Assert.Empty(ex.Errors);
    }

    // #14: Fehlertexte nach Ursache (Netzwerk vs. 404 vs. Serverfehler), deutsch.
    [Fact]
    public async Task NotFound_ohne_Detail_liefert_deutsche_Meldung()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, """{"title":"Not Found","status":404}""");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetConferenceAsync(4711));

        Assert.True(ex.IsNotFound);
        Assert.Contains("nicht gefunden", ex.Message);
        Assert.DoesNotContain("Not Found", ex.Message);
    }

    [Fact]
    public async Task Leerer_Body_bei_404_liefert_Nicht_gefunden()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.DeleteSpeakerAsync(1));
        Assert.Contains("nicht gefunden", ex.Message);
    }

    [Fact]
    public async Task Serverfehler_liefert_deutsche_Meldung_mit_Statuscode_statt_englischem_Titel()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, """{"title":"An error occurred while processing your request.","status":500}""");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetSpeakersAsync());
        Assert.Contains("500", ex.Message);
        Assert.DoesNotContain("An error occurred", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Gateway_Fehler_gelten_als_nicht_erreichbar(HttpStatusCode status)
    {
        var (client, _) = Create(status, "<html>Bad Gateway</html>", "text/html");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetSpeakersAsync());
        Assert.Equal(status, ex.Status);
        Assert.Contains("nicht erreichbar", ex.Message);
    }

    [Fact]
    public async Task Unbekannter_Client_Fehler_ohne_Body_liefert_Statuscode()
    {
        var (client, _) = Create(HttpStatusCode.Forbidden, "<html>nein</html>", "text/html");
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetSpeakersAsync());
        Assert.Contains("403", ex.Message);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task Netzwerkfehler_und_Timeout_liefern_ServiceUnavailable(Type exceptionType)
    {
        var handler = new FakeHandler(_ => throw (Exception)Activator.CreateInstance(exceptionType)!);
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetConferencesAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.Status);
        Assert.Contains("nicht erreichbar", ex.Message);
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
