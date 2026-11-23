using System.Net;
using System.Net.Http.Json;
using ConferenceApp.E2ETests.Infrastructure;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.E2ETests.Api;

public class SessionApiTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Konferenz Day1–Day3 mit einem Raum.</summary>
    private async Task<(ConferenceDto Conf, RoomDto Room)> SetupAsync()
    {
        var conf = await CreateConferenceAsync(Day1, Day3);
        var room = await CreateRoomAsync(conf.Id);
        return (conf, room);
    }

    [Fact]
    public async Task Anlegen_mit_Raum_Track_und_Speakern_wird_gespeichert()
    {
        var (conf, room) = await SetupAsync();
        var track = await CreateTrackAsync(conf.Id, "Cloud", "#00897B");
        var zoe = await CreateSpeakerAsync("Zoe Zander");
        var anna = await CreateSpeakerAsync("Anna Adler");

        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day2, 9, 0, 9, 45, track.Id, [zoe.Id, anna.Id], "Mein Talk"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!;
        Assert.Equal($"/api/conferences/{conf.Id}/sessions/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(("Mein Talk", Day2, new TimeOnly(9, 0), new TimeOnly(9, 45)), (created.Title, created.Day, created.StartTime, created.EndTime));
        Assert.Equal((room.Id, room.Name), (created.RoomId, created.RoomName));
        Assert.Equal((track.Id, "Cloud", "#00897B"), (created.TrackId, created.TrackName, created.TrackColor));
        Assert.Equal(["Anna Adler", "Zoe Zander"], created.Speakers.Select(s => s.Name));

        var loaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{created.Id}");
        Assert.Equal(created.Speakers.Select(s => s.Id), loaded.Speakers.Select(s => s.Id));

        var agenda = await GetAsync<AgendaDto>($"api/conferences/{conf.Slug}/agenda");
        Assert.Equal(created.Id, Assert.Single(agenda.Sessions).Id);

        var speakerDetail = await GetAsync<SpeakerDetailDto>($"api/speakers/{anna.Id}");
        var s = Assert.Single(speakerDetail.Sessions);
        Assert.Equal((created.Id, conf.Slug, room.Name), (s.Id, s.ConferenceSlug, s.RoomName));
    }

    [Fact]
    public async Task Ende_gleich_Beginn_ist_400()
    {
        var (conf, room) = await SetupAsync();
        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day1, 10, 0, 10, 0));
        await AssertValidationProblemAsync(response, "EndTime");
    }

    [Fact]
    public async Task Ende_vor_Beginn_ist_400()
    {
        var (conf, room) = await SetupAsync();
        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day1, 11, 0, 10, 0));
        await AssertValidationProblemAsync(response, "EndTime");
    }

    [Fact]
    public async Task Session_am_ersten_und_letzten_Konferenztag_ist_erlaubt()
    {
        var (conf, room) = await SetupAsync();
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));
        await CreateSessionAsync(conf.Id, Session(room.Id, Day3, 9, 0, 10, 0));
    }

    [Fact]
    public async Task Session_einen_Tag_vor_Beginn_ist_400()
    {
        var (conf, room) = await SetupAsync();
        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day1.AddDays(-1), 9, 0, 10, 0));
        await AssertValidationProblemAsync(response, "Day");
    }

    [Fact]
    public async Task Session_einen_Tag_nach_Ende_ist_400()
    {
        var (conf, room) = await SetupAsync();
        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day3.AddDays(1), 9, 0, 10, 0));
        await AssertValidationProblemAsync(response, "Day");
    }

    [Fact]
    public async Task Anschliessende_Sessions_im_selben_Raum_ueberschneiden_sich_nicht()
    {
        var (conf, room) = await SetupAsync();
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));

        // endet genau zum Beginn der ersten bzw. beginnt genau zu ihrem Ende
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 10, 0, 11, 0));
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 8, 0, 9, 0));
    }

    [Theory]
    [InlineData(9, 30, 10, 30)]   // überlappt das Ende
    [InlineData(8, 30, 9, 30)]    // überlappt den Beginn
    [InlineData(9, 15, 9, 45)]    // liegt innerhalb
    [InlineData(8, 0, 11, 0)]     // umschließt
    [InlineData(9, 0, 10, 0)]     // identisch
    [InlineData(9, 59, 10, 30)]   // eine Minute Überschneidung
    public async Task Ueberschneidung_im_selben_Raum_ist_400(int sh, int sm, int eh, int em)
    {
        var (conf, room) = await SetupAsync();
        var first = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, title: "Erste Session"));

        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day1, sh, sm, eh, em));

        var messages = await AssertValidationProblemAsync(response, "StartTime");
        Assert.Contains(messages, m => m.Contains(first.Title));
        var count = await Fixture.WithDbAsync(db => db.Sessions.CountAsync(s => s.ConferenceId == conf.Id, Ct));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Gleiche_Zeit_in_anderem_Raum_oder_an_anderem_Tag_ist_erlaubt()
    {
        var (conf, room) = await SetupAsync();
        var room2 = await CreateRoomAsync(conf.Id, "Raum 2");
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));

        await CreateSessionAsync(conf.Id, Session(room2.Id, Day1, 9, 0, 10, 0));
        await CreateSessionAsync(conf.Id, Session(room.Id, Day2, 9, 0, 10, 0));
    }

    [Fact]
    public async Task Aendern_ohne_Zeitwechsel_kollidiert_nicht_mit_sich_selbst()
    {
        var (conf, room) = await SetupAsync();
        var speaker = await CreateSpeakerAsync();
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, speakerIds: [speaker.Id]));

        var update = Session(room.Id, Day1, 9, 0, 10, 0, speakerIds: [speaker.Id], title: "Neuer Titel");
        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}/sessions/{session.Id}", update, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{session.Id}");
        Assert.Equal("Neuer Titel", reloaded.Title);
        Assert.Equal([speaker.Id], reloaded.Speakers.Select(s => s.Id));
    }

    [Fact]
    public async Task Aendern_ersetzt_die_Speaker_Zuordnung()
    {
        var (conf, room) = await SetupAsync();
        var a = await CreateSpeakerAsync();
        var b = await CreateSpeakerAsync();
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, speakerIds: [a.Id]));

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}/sessions/{session.Id}",
            Session(room.Id, Day1, 9, 0, 10, 0, speakerIds: [b.Id]), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{session.Id}");
        Assert.Equal([b.Id], reloaded.Speakers.Select(s => s.Id));
    }

    [Fact]
    public async Task Aendern_in_eine_Ueberschneidung_ist_400_und_aendert_nichts()
    {
        var (conf, room) = await SetupAsync();
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));
        var second = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 10, 0, 11, 0));

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}/sessions/{second.Id}",
            Session(room.Id, Day1, 9, 30, 10, 30, title: second.Title), Ct);

        await AssertValidationProblemAsync(response, "StartTime");
        var reloaded = await GetAsync<SessionDto>($"api/conferences/{conf.Id}/sessions/{second.Id}");
        Assert.Equal(new TimeOnly(10, 0), reloaded.StartTime);
    }

    [Fact]
    public async Task Raum_einer_anderen_Konferenz_ist_400()
    {
        var (conf, _) = await SetupAsync();
        var (_, foreignRoom) = await SetupAsync();

        var response = await PostSessionAsync(conf.Id, Session(foreignRoom.Id, Day1, 9, 0, 10, 0));

        await AssertValidationProblemAsync(response, "RoomId");
    }

    [Fact]
    public async Task Track_einer_anderen_Konferenz_ist_400()
    {
        var (conf, room) = await SetupAsync();
        var other = await CreateConferenceAsync();
        var foreignTrack = await CreateTrackAsync(other.Id);

        var response = await PostSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, foreignTrack.Id));

        await AssertValidationProblemAsync(response, "TrackId");
    }

    [Fact]
    public async Task Unbekannter_Raum_oder_Speaker_ist_400()
    {
        var (conf, room) = await SetupAsync();
        await AssertValidationProblemAsync(await PostSessionAsync(conf.Id, Session(999999, Day1, 9, 0, 10, 0)), "RoomId");
        await AssertValidationProblemAsync(await PostSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, speakerIds: [999999])), "SpeakerIds");
    }

    [Fact]
    public async Task Fehlender_Titel_ist_400()
    {
        var (conf, room) = await SetupAsync();
        var request = Session(room.Id, Day1, 9, 0, 10, 0);
        request.Title = "";
        await AssertValidationProblemAsync(await PostSessionAsync(conf.Id, request), "Title");
    }

    [Fact]
    public async Task Unbekannte_IDs_liefern_404()
    {
        var (conf, room) = await SetupAsync();
        var (other, otherRoom) = await SetupAsync();
        var foreignSession = await CreateSessionAsync(other.Id, Session(otherRoom.Id, Day1, 9, 0, 10, 0));

        await AssertProblemAsync(await PostSessionAsync(999999, Session(room.Id, Day1, 9, 0, 10, 0)), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.GetAsync($"api/conferences/{conf.Id}/sessions/999999", Ct), HttpStatusCode.NotFound);
        // Session existiert, gehört aber zu einer anderen Konferenz
        await AssertProblemAsync(await Http.GetAsync($"api/conferences/{conf.Id}/sessions/{foreignSession.Id}", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.PutAsJsonAsync($"api/conferences/{conf.Id}/sessions/{foreignSession.Id}",
            Session(room.Id, Day1, 9, 0, 10, 0), Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.DeleteAsync($"api/conferences/{conf.Id}/sessions/{foreignSession.Id}", Ct), HttpStatusCode.NotFound);

        // Die fremde Session ist unverändert vorhanden
        await GetAsync<SessionDto>($"api/conferences/{other.Id}/sessions/{foreignSession.Id}");
    }

    [Fact]
    public async Task Loeschen_entfernt_die_Session()
    {
        var (conf, room) = await SetupAsync();
        var session = await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));

        await AssertStatusAsync(HttpStatusCode.NoContent, Http.DeleteAsync($"api/conferences/{conf.Id}/sessions/{session.Id}", Ct));

        await AssertStatusAsync(HttpStatusCode.NotFound, Http.GetAsync($"api/conferences/{conf.Id}/sessions/{session.Id}", Ct));
        var exists = await Fixture.WithDbAsync(db => db.Sessions.AnyAsync(s => s.Id == session.Id, Ct));
        Assert.False(exists);
    }
}
