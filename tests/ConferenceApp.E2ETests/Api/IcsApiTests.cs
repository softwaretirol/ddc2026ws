using System.Net;
using ConferenceApp.E2ETests.Infrastructure;

namespace ConferenceApp.E2ETests.Api;

/// <summary>#10: GET /api/conferences/{slug}/agenda.ics – text/calendar, abonnierbar, optional ?track=.</summary>
public class IcsApiTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static List<string> Unfold(string ics) =>
        ics.Replace("\r\n ", "").Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();

    [Fact]
    public async Task Agenda_als_Kalender_mit_allen_Sessions_der_Konferenz()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id, "Saal");
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, title: "Erster Talk"));
        await CreateSessionAsync(conf.Id, Session(room.Id, Day2, 14, 30, 15, 15, title: "Zweiter Talk"));
        var other = await CreateConferenceAsync();
        var otherRoom = await CreateRoomAsync(other.Id);
        await CreateSessionAsync(other.Id, Session(otherRoom.Id, Day1, 9, 0, 10, 0, title: "Fremder Talk"));

        var response = await Http.GetAsync($"api/conferences/{conf.Slug}/agenda.ics", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        var lines = Unfold(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.Equal(["SUMMARY:Erster Talk", "SUMMARY:Zweiter Talk"], lines.Where(l => l.StartsWith("SUMMARY:")));
        Assert.Contains("DTSTART:20270511T143000", lines);   // floating, ohne Z
    }

    [Fact]
    public async Task Filter_nach_Track()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        var track = await CreateTrackAsync(conf.Id, "Cloud");
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, track.Id, title: "Mit Track"));
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 10, 0, 11, 0, title: "Ohne Track"));

        var ics = await Http.GetStringAsync($"api/conferences/{conf.Slug}/agenda.ics?track={track.Id}", Ct);

        Assert.Equal(["SUMMARY:Mit Track"], Unfold(ics).Where(l => l.StartsWith("SUMMARY:")));
    }

    [Fact]
    public async Task Unbekannter_Slug_ist_404()
    {
        var response = await Http.GetAsync("api/conferences/gibt-es-nicht/agenda.ics", Ct);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Track_einer_anderen_Konferenz_ist_404()
    {
        var conf = await CreateConferenceAsync();
        var other = await CreateConferenceAsync();
        var foreignTrack = await CreateTrackAsync(other.Id);

        var response = await Http.GetAsync($"api/conferences/{conf.Slug}/agenda.ics?track={foreignTrack.Id}", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Leere_Konferenz_liefert_gueltigen_leeren_Kalender()
    {
        var conf = await CreateConferenceAsync();
        var lines = Unfold(await Http.GetStringAsync($"api/conferences/{conf.Slug}/agenda.ics", Ct));
        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.DoesNotContain("BEGIN:VEVENT", lines);
    }
}
