using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConferenceApp.Shared;

namespace ConferenceApp.E2ETests.Infrastructure;

/// <summary>Gemeinsame Helfer für API-Tests: eigene Testdaten pro Test (eindeutige Slugs), ProblemDetails lesen.</summary>
public abstract class ApiTestBase(E2EFixture fixture) : IDisposable
{
    protected E2EFixture Fixture { get; } = fixture;
    protected HttpClient Http { get; } = fixture.CreateApiClient();

    /// <summary>Standard-Zeitraum der Testkonferenzen: 3 Tage.</summary>
    protected static readonly DateOnly Day1 = new(2027, 5, 10);
    protected static readonly DateOnly Day2 = Day1.AddDays(1);
    protected static readonly DateOnly Day3 = Day1.AddDays(2);

    protected static string UniqueSlug(string prefix = "test") => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 100)];

    protected static ConferenceRequest ConferenceRequest(string? slug = null, DateOnly? start = null, DateOnly? end = null) => new()
    {
        Name = "Testkonferenz " + Guid.NewGuid().ToString("N")[..6],
        Slug = slug ?? UniqueSlug(),
        Description = "Für Tests",
        StartDate = start ?? Day1,
        EndDate = end ?? Day3,
        Location = "Testort",
    };

    protected async Task<ConferenceDto> CreateConferenceAsync(DateOnly? start = null, DateOnly? end = null)
    {
        var response = await Http.PostAsJsonAsync("api/conferences", ConferenceRequest(start: start, end: end), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConferenceDto>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<RoomDto> CreateRoomAsync(int conferenceId, string name = "Saal")
    {
        var response = await Http.PostAsJsonAsync($"api/conferences/{conferenceId}/rooms", new RoomRequest { Name = name, Capacity = 100 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoomDto>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<TrackDto> CreateTrackAsync(int conferenceId, string name = ".NET", string color = "#512BD4")
    {
        var response = await Http.PostAsJsonAsync($"api/conferences/{conferenceId}/tracks", new TrackRequest { Name = name, Color = color }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TrackDto>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<SpeakerDto> CreateSpeakerAsync(string? name = null)
    {
        var response = await Http.PostAsJsonAsync("api/speakers", new SpeakerRequest { Name = name ?? "Speaker " + Guid.NewGuid().ToString("N")[..6] }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SpeakerDto>(TestContext.Current.CancellationToken))!;
    }

    protected static SessionRequest Session(int roomId, DateOnly day, int startHour, int startMinute, int endHour, int endMinute,
        int? trackId = null, List<int>? speakerIds = null, string? title = null) => new()
    {
        Title = title ?? "Session " + Guid.NewGuid().ToString("N")[..6],
        Abstract = "Abstract",
        Day = day,
        StartTime = new TimeOnly(startHour, startMinute),
        EndTime = new TimeOnly(endHour, endMinute),
        RoomId = roomId,
        TrackId = trackId,
        SpeakerIds = speakerIds ?? [],
    };

    protected Task<HttpResponseMessage> PostSessionAsync(int conferenceId, SessionRequest request) =>
        Http.PostAsJsonAsync($"api/conferences/{conferenceId}/sessions", request, TestContext.Current.CancellationToken);

    protected async Task<SessionDto> CreateSessionAsync(int conferenceId, SessionRequest request)
    {
        var response = await PostSessionAsync(conferenceId, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>(TestContext.Current.CancellationToken))!;
    }

    protected async Task<T> GetAsync<T>(string url)
    {
        var response = await Http.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    protected async Task AssertStatusAsync(HttpStatusCode expected, Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>
    /// Erwartet 400 als ValidationProblem (application/problem+json mit "errors") mit einem Fehler am angegebenen Feld.
    /// Liefert die Fehlermeldungen dieses Felds.
    /// </summary>
    protected static async Task<string[]> AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Erwartet 400, war {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors), $"ProblemDetails ohne 'errors': {body}");
        var match = errors.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));
        Assert.True(match.Value.ValueKind == JsonValueKind.Array, $"Kein Fehler für Feld '{field}': {body}");
        var messages = match.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.NotEmpty(messages);
        return messages;
    }

    /// <summary>Erwartet ProblemDetails mit dem angegebenen Status; liefert das JSON-Dokument.</summary>
    protected static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == status, $"Erwartet {(int)status}, war {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal((int)status, doc.RootElement.GetProperty("status").GetInt32());
        return doc.RootElement.Clone();
    }

    public void Dispose()
    {
        Http.Dispose();
        GC.SuppressFinalize(this);
    }
}
