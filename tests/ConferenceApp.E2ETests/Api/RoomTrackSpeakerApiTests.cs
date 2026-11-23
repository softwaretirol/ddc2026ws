using System.Net;
using System.Net.Http.Json;
using ConferenceApp.E2ETests.Infrastructure;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.E2ETests.Api;

public class RoomApiTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Raum_anlegen_aendern_und_lesen()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id, "  Saal Tirol  ");
        Assert.Equal("Saal Tirol", room.Name);

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", new RoomRequest { Name = "Saal Wien", Capacity = 250 }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rooms = await GetAsync<List<RoomDto>>($"api/conferences/{conf.Id}/rooms");
        var reloaded = Assert.Single(rooms);
        Assert.Equal(("Saal Wien", 250), (reloaded.Name, reloaded.Capacity));
    }

    [Fact]
    public async Task Raum_mit_Sessions_loeschen_ist_409_und_Raum_bleibt()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));

        var response = await Http.DeleteAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        await GetAsync<RoomDto>($"api/conferences/{conf.Id}/rooms/{room.Id}");
    }

    [Fact]
    public async Task Leeren_Raum_loeschen_ist_204()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);

        await AssertStatusAsync(HttpStatusCode.NoContent, Http.DeleteAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct));
        await AssertProblemAsync(await Http.GetAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.DeleteAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Raum_nach_Loeschen_seiner_Sessions_loeschbar()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));

        await AssertStatusAsync(HttpStatusCode.Conflict, Http.DeleteAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct));
        await AssertStatusAsync(HttpStatusCode.NoContent, Http.DeleteAsync($"api/conferences/{conf.Id}/sessions/{session.Id}", Ct));
        await AssertStatusAsync(HttpStatusCode.NoContent, Http.DeleteAsync($"api/conferences/{conf.Id}/rooms/{room.Id}", Ct));
    }

    [Fact]
    public async Task Raum_einer_anderen_Konferenz_ist_404_auch_wenn_er_Sessions_hat()
    {
        var owner = await CreateConferenceAsync();
        var room = await CreateRoomAsync(owner.Id);
        await CreateSessionAsync(owner.Id, Session(room.Id, Day1, 9, 0, 10, 0));
        var other = await CreateConferenceAsync();

        // Über eine fremde Konferenz ist der Raum nicht adressierbar – weder lesen, ändern noch löschen.
        await AssertProblemAsync(await Http.GetAsync($"api/conferences/{other.Id}/rooms/{room.Id}", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.PutAsJsonAsync($"api/conferences/{other.Id}/rooms/{room.Id}", new RoomRequest { Name = "X" }, Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.DeleteAsync($"api/conferences/{other.Id}/rooms/{room.Id}", Ct), HttpStatusCode.NotFound);

        var reloaded = await GetAsync<RoomDto>($"api/conferences/{owner.Id}/rooms/{room.Id}");
        Assert.Equal("Saal", reloaded.Name);
    }

    [Fact]
    public async Task Raum_fuer_unbekannte_Konferenz_ist_404()
    {
        var response = await Http.PostAsJsonAsync("api/conferences/999999/rooms", new RoomRequest { Name = "Saal" }, Ct);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Kapazitaet_kleiner_1_ist_400(int capacity)
    {
        var conf = await CreateConferenceAsync();
        var response = await Http.PostAsJsonAsync($"api/conferences/{conf.Id}/rooms", new RoomRequest { Name = "Saal", Capacity = capacity }, Ct);
        await AssertValidationProblemAsync(response, "Capacity");
    }
}

public class TrackApiTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Track_loeschen_Sessions_bleiben_ohne_Track()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        var track = await CreateTrackAsync(conf.Id);
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, track.Id));

        await AssertStatusAsync(HttpStatusCode.NoContent, Http.DeleteAsync($"api/conferences/{conf.Id}/tracks/{track.Id}", Ct));

        var reloaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{session.Id}");
        Assert.Null(reloaded.TrackId);
        Assert.Null(reloaded.TrackName);
        var agenda = await GetAsync<AgendaDto>($"api/conferences/{conf.Slug}/agenda");
        Assert.Empty(agenda.Tracks);
        Assert.Equal(session.Id, Assert.Single(agenda.Sessions).Id);
    }

    [Fact]
    public async Task Track_aendern_wirkt_auf_Sessions()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        var track = await CreateTrackAsync(conf.Id, "Alt", "#111111");
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, track.Id));

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}/tracks/{track.Id}", new TrackRequest { Name = "Neu", Color = "#222222" }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reloaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{session.Id}");
        Assert.Equal(("Neu", "#222222"), (reloaded.TrackName, reloaded.TrackColor));
    }

    [Fact]
    public async Task Ungueltige_Farbe_ist_400()
    {
        var conf = await CreateConferenceAsync();
        var response = await Http.PostAsJsonAsync($"api/conferences/{conf.Id}/tracks", new TrackRequest { Name = "T", Color = "blau" }, Ct);
        await AssertValidationProblemAsync(response, "Color");
    }

    [Fact]
    public async Task Track_einer_anderen_Konferenz_oder_unbekannt_ist_404()
    {
        var owner = await CreateConferenceAsync();
        var track = await CreateTrackAsync(owner.Id);
        var other = await CreateConferenceAsync();

        await AssertProblemAsync(await Http.GetAsync($"api/conferences/{other.Id}/tracks/{track.Id}", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.DeleteAsync($"api/conferences/{other.Id}/tracks/{track.Id}", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.PostAsJsonAsync("api/conferences/999999/tracks", new TrackRequest { Name = "T" }, Ct), HttpStatusCode.NotFound);
        await GetAsync<TrackDto>($"api/conferences/{owner.Id}/tracks/{track.Id}");
    }
}

public class SpeakerApiTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Speaker_anlegen_bereinigt_Links_und_Foto()
    {
        var request = new SpeakerRequest
        {
            Name = "  Anna Adler  ",
            Company = "Contoso",
            PhotoUrl = "",
            Links = ["https://a.example", "  ", " https://b.example "],
        };

        var response = await Http.PostAsJsonAsync("api/speakers", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<SpeakerDto>(Ct))!;
        Assert.Equal("Anna Adler", created.Name);
        Assert.Null(created.PhotoUrl);
        Assert.Equal(["https://a.example", "https://b.example"], created.Links);

        var detail = await GetAsync<SpeakerDetailDto>($"api/speakers/{created.Id}");
        Assert.Equal(created.Links, detail.Speaker.Links);
        Assert.Empty(detail.Sessions);
    }

    [Fact]
    public async Task Ungueltige_Foto_URL_ist_400()
    {
        var response = await Http.PostAsJsonAsync("api/speakers", new SpeakerRequest { Name = "A", PhotoUrl = "kein link" }, Ct);
        await AssertValidationProblemAsync(response, "PhotoUrl");
    }

    [Fact]
    public async Task Speaker_loeschen_entfernt_Zuordnung_Session_bleibt()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        var gone = await CreateSpeakerAsync();
        var stays = await CreateSpeakerAsync();
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, speakerIds: [gone.Id, stays.Id]));

        await AssertStatusAsync(HttpStatusCode.NoContent, Http.DeleteAsync($"api/speakers/{gone.Id}", Ct));

        var reloaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{session.Id}");
        Assert.Equal([stays.Id], reloaded.Speakers.Select(s => s.Id));
        await AssertProblemAsync(await Http.GetAsync($"api/speakers/{gone.Id}", Ct), HttpStatusCode.NotFound);
        var speakers = await GetAsync<List<SpeakerDto>>("api/speakers");
        Assert.DoesNotContain(speakers, s => s.Id == gone.Id);
    }

    [Fact]
    public async Task Speaker_aendern_wird_gespeichert()
    {
        var speaker = await CreateSpeakerAsync();
        var response = await Http.PutAsJsonAsync($"api/speakers/{speaker.Id}",
            new SpeakerRequest { Name = "Neuer Name", Bio = "Bio", Company = "Fabrikam", PhotoUrl = "https://example.com/p.png" }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var inDb = await Fixture.WithDbAsync(db => db.Speakers.AsNoTracking().SingleAsync(s => s.Id == speaker.Id, Ct));
        Assert.Equal(("Neuer Name", "Bio", "Fabrikam", "https://example.com/p.png"), (inDb.Name, inDb.Bio, inDb.Company, inDb.PhotoUrl));
    }

    [Fact]
    public async Task Unbekannter_Speaker_ist_404()
    {
        await AssertProblemAsync(await Http.GetAsync("api/speakers/999999", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.PutAsJsonAsync("api/speakers/999999", new SpeakerRequest { Name = "X" }, Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.DeleteAsync("api/speakers/999999", Ct), HttpStatusCode.NotFound);
    }
}
