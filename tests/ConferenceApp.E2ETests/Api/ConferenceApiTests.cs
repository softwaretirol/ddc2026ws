using System.Net;
using System.Net.Http.Json;
using ConferenceApp.E2ETests.Infrastructure;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.E2ETests.Api;

public class ConferenceApiTests(E2EFixture fixture) : ApiTestBase(fixture)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_Konferenz_ist_vorhanden_und_ihre_Agenda_abrufbar()
    {
        var list = await GetAsync<List<ConferenceDto>>("api/conferences");
        var seed = Assert.Single(list, c => c.Slug == E2EFixture.SeedSlug);
        Assert.Equal("Dev Conference 2026", seed.Name);

        var agenda = await GetAsync<AgendaDto>($"api/conferences/{E2EFixture.SeedSlug}/agenda");
        Assert.Equal(seed.Id, agenda.Conference.Id);
        Assert.Equal(3, agenda.Rooms.Count);
        Assert.Equal(3, agenda.Tracks.Count);
        Assert.NotEmpty(agenda.Sessions);
        Assert.All(agenda.Sessions, s => Assert.InRange(s.Day, seed.StartDate, seed.EndDate));
        // Chronologisch sortiert
        Assert.Equal(agenda.Sessions.OrderBy(s => s.Day).ThenBy(s => s.StartTime).Select(s => s.Id), agenda.Sessions.Select(s => s.Id));
    }

    [Fact]
    public async Task Anlegen_liefert_201_mit_Location_und_wird_gespeichert()
    {
        var request = ConferenceRequest();
        var response = await Http.PostAsJsonAsync("api/conferences", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ConferenceDto>(Ct))!;
        Assert.Equal($"/api/conferences/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal((request.Name, request.Slug, request.StartDate, request.EndDate, request.Location, request.Description),
            (created.Name, created.Slug, created.StartDate, created.EndDate, created.Location, created.Description));

        // Persistenz: per GET und direkt in der Datenbank
        var loaded = await GetAsync<ConferenceDto>($"api/conferences/{created.Id}");
        Assert.Equal(created, loaded);
        var inDb = await Fixture.WithDbAsync(db => db.Conferences.AsNoTracking().SingleAsync(c => c.Id == created.Id, Ct));
        Assert.Equal(request.Slug, inDb.Slug);

        var agenda = await GetAsync<AgendaDto>($"api/conferences/{request.Slug}/agenda");
        Assert.Equal(created.Id, agenda.Conference.Id);
        Assert.Empty(agenda.Sessions);
    }

    [Fact]
    public async Task Doppelter_Slug_beim_Anlegen_ist_400()
    {
        var existing = await CreateConferenceAsync();

        var response = await Http.PostAsJsonAsync("api/conferences", ConferenceRequest(slug: existing.Slug), Ct);

        await AssertValidationProblemAsync(response, "Slug");
        var count = await Fixture.WithDbAsync(db => db.Conferences.CountAsync(c => c.Slug == existing.Slug, Ct));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Seed_Slug_kann_nicht_erneut_vergeben_werden()
    {
        var response = await Http.PostAsJsonAsync("api/conferences", ConferenceRequest(slug: E2EFixture.SeedSlug), Ct);
        await AssertValidationProblemAsync(response, "Slug");
    }

    [Fact]
    public async Task Doppelter_Slug_beim_Aendern_ist_400()
    {
        var a = await CreateConferenceAsync();
        var b = await CreateConferenceAsync();

        var response = await Http.PutAsJsonAsync($"api/conferences/{b.Id}", ConferenceRequest(slug: a.Slug), Ct);

        await AssertValidationProblemAsync(response, "Slug");
        var reloaded = await GetAsync<ConferenceDto>($"api/conferences/{b.Id}");
        Assert.Equal(b.Slug, reloaded.Slug);
    }

    [Fact]
    public async Task Aendern_mit_eigenem_Slug_ist_erlaubt_und_wird_gespeichert()
    {
        var conf = await CreateConferenceAsync();
        var request = ConferenceRequest(slug: conf.Slug);
        request.Name = "Umbenannt";
        request.Location = "Wien";

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}", request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await GetAsync<ConferenceDto>($"api/conferences/{conf.Id}");
        Assert.Equal(("Umbenannt", "Wien", conf.Slug), (reloaded.Name, reloaded.Location, reloaded.Slug));
    }

    [Fact]
    public async Task Slug_aendern_alte_Agenda_URL_ist_404_neue_funktioniert()
    {
        var conf = await CreateConferenceAsync();
        var newSlug = UniqueSlug("neu");

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}", ConferenceRequest(slug: newSlug), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertStatusAsync(HttpStatusCode.NotFound, Http.GetAsync($"api/conferences/{conf.Slug}/agenda", Ct));
        await AssertStatusAsync(HttpStatusCode.OK, Http.GetAsync($"api/conferences/{newSlug}/agenda", Ct));
    }

    [Theory]
    [InlineData("Gross-Buchstaben")]
    [InlineData("mit leerzeichen")]
    [InlineData("-vorne")]
    [InlineData("hinten-")]
    [InlineData("doppel--strich")]
    [InlineData("unter_strich")]
    [InlineData("")]
    public async Task Ungueltiger_Slug_ist_400(string slug)
    {
        var response = await Http.PostAsJsonAsync("api/conferences", ConferenceRequest(slug: slug), Ct);
        await AssertValidationProblemAsync(response, "Slug");
    }

    [Fact]
    public async Task Enddatum_vor_Startdatum_ist_400()
    {
        var response = await Http.PostAsJsonAsync("api/conferences", ConferenceRequest(start: Day2, end: Day1), Ct);
        await AssertValidationProblemAsync(response, "EndDate");
    }

    [Fact]
    public async Task Enddatum_gleich_Startdatum_ist_erlaubt()
    {
        var response = await Http.PostAsJsonAsync("api/conferences", ConferenceRequest(start: Day1, end: Day1), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Fehlender_Name_liefert_ValidationProblem_mit_errors()
    {
        var request = ConferenceRequest();
        request.Name = "";

        var response = await Http.PostAsJsonAsync("api/conferences", request, Ct);

        var messages = await AssertValidationProblemAsync(response, "Name");
        Assert.Contains("Name ist erforderlich.", messages);
    }

    [Fact]
    public async Task Zeitraum_verkuerzen_mit_Sessions_ausserhalb_ist_400_und_aendert_nichts()
    {
        var conf = await CreateConferenceAsync(Day1, Day3);
        var room = await CreateRoomAsync(conf.Id);
        await CreateSessionAsync(conf.Id, Session(room.Id, Day3, 9, 0, 10, 0));

        var request = ConferenceRequest(slug: conf.Slug, start: Day1, end: Day2);
        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}", request, Ct);

        await AssertValidationProblemAsync(response, "EndDate");
        var reloaded = await GetAsync<ConferenceDto>($"api/conferences/{conf.Id}");
        Assert.Equal(Day3, reloaded.EndDate);
    }

    [Fact]
    public async Task Beginn_nach_hinten_verschieben_mit_Session_am_ersten_Tag_ist_400()
    {
        var conf = await CreateConferenceAsync(Day1, Day3);
        var room = await CreateRoomAsync(conf.Id);
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0));

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}", ConferenceRequest(slug: conf.Slug, start: Day2, end: Day3), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reloaded = await GetAsync<ConferenceDto>($"api/conferences/{conf.Id}");
        Assert.Equal(Day1, reloaded.StartDate);
    }

    [Fact]
    public async Task Zeitraum_verkuerzen_ohne_betroffene_Sessions_ist_erlaubt()
    {
        var conf = await CreateConferenceAsync(Day1, Day3);
        var room = await CreateRoomAsync(conf.Id);
        await CreateSessionAsync(conf.Id, Session(room.Id, Day2, 9, 0, 10, 0));

        var response = await Http.PutAsJsonAsync($"api/conferences/{conf.Id}", ConferenceRequest(slug: conf.Slug, start: Day2, end: Day2), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await GetAsync<ConferenceDto>($"api/conferences/{conf.Id}");
        Assert.Equal((Day2, Day2), (reloaded.StartDate, reloaded.EndDate));
    }

    [Fact]
    public async Task Loeschen_entfernt_Konferenz_mit_Tracks_Raeumen_und_Sessions_Speaker_bleiben()
    {
        var conf = await CreateConferenceAsync();
        var room = await CreateRoomAsync(conf.Id);
        var track = await CreateTrackAsync(conf.Id);
        var speaker = await CreateSpeakerAsync();
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 9, 0, 10, 0, track.Id, [speaker.Id]));
        await CreateSessionAsync(conf.Id, Session(room.Id, Day1, 10, 0, 11, 0, track.Id, [speaker.Id]));

        var response = await Http.DeleteAsync($"api/conferences/{conf.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertStatusAsync(HttpStatusCode.NotFound, Http.GetAsync($"api/conferences/{conf.Id}", Ct));
        await AssertStatusAsync(HttpStatusCode.NotFound, Http.GetAsync($"api/conferences/{conf.Slug}/agenda", Ct));

        var remaining = await Fixture.WithDbAsync(async db => (
            Rooms: await db.Rooms.CountAsync(r => r.ConferenceId == conf.Id, Ct),
            Tracks: await db.Tracks.CountAsync(t => t.ConferenceId == conf.Id, Ct),
            Sessions: await db.Sessions.CountAsync(s => s.ConferenceId == conf.Id, Ct)));
        Assert.Equal((0, 0, 0), remaining);

        var speakerDetail = await GetAsync<SpeakerDetailDto>($"api/speakers/{speaker.Id}");
        Assert.Empty(speakerDetail.Sessions);
    }

    [Fact]
    public async Task Unbekannte_Konferenz_liefert_404_als_ProblemDetails()
    {
        await AssertProblemAsync(await Http.GetAsync("api/conferences/999999", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.GetAsync("api/conferences/gibt-es-nicht/agenda", Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.PutAsJsonAsync("api/conferences/999999", ConferenceRequest(), Ct), HttpStatusCode.NotFound);
        await AssertProblemAsync(await Http.DeleteAsync("api/conferences/999999", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Agenda_enthaelt_nur_Daten_der_eigenen_Konferenz()
    {
        var a = await CreateConferenceAsync();
        var b = await CreateConferenceAsync();
        var roomA = await CreateRoomAsync(a.Id, "Raum A");
        var roomB = await CreateRoomAsync(b.Id, "Raum B");
        await CreateTrackAsync(b.Id, "Nur B");
        await CreateSessionAsync(a.Id, Session(roomA.Id, Day1, 9, 0, 10, 0, title: "Nur in A"));
        await CreateSessionAsync(b.Id, Session(roomB.Id, Day1, 9, 0, 10, 0, title: "Nur in B"));

        var agenda = await GetAsync<AgendaDto>($"api/conferences/{a.Slug}/agenda");

        Assert.Equal(["Raum A"], agenda.Rooms.Select(r => r.Name));
        Assert.Empty(agenda.Tracks);
        Assert.Equal(["Nur in A"], agenda.Sessions.Select(s => s.Title));
    }
}
