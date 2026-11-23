using System.Net;
using System.Net.Http.Json;
using ConferenceApp.Shared;

namespace ConferenceApp.Client.Services;

/// <summary>Fehler der API, aufbereitet aus ProblemDetails.</summary>
public class ApiException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public bool IsNotFound => Status == HttpStatusCode.NotFound;
}

/// <summary>Typisierter Zugriff auf die Konferenz-API.</summary>
public class ApiClient(HttpClient http)
{
    // Konferenzen
    public Task<List<ConferenceDto>> GetConferencesAsync() => GetAsync<List<ConferenceDto>>("api/conferences");
    public Task<ConferenceDto> GetConferenceAsync(int id) => GetAsync<ConferenceDto>($"api/conferences/{id}");
    public Task<AgendaDto> GetAgendaAsync(string slug) => GetAsync<AgendaDto>($"api/conferences/{Uri.EscapeDataString(slug)}/agenda");
    public Task<ConferenceDto> CreateConferenceAsync(ConferenceRequest r) => SendAsync<ConferenceDto>(HttpMethod.Post, "api/conferences", r);
    public Task<ConferenceDto> UpdateConferenceAsync(int id, ConferenceRequest r) => SendAsync<ConferenceDto>(HttpMethod.Put, $"api/conferences/{id}", r);
    public Task DeleteConferenceAsync(int id) => DeleteAsync($"api/conferences/{id}");

    // Tracks
    public Task<List<TrackDto>> GetTracksAsync(int conferenceId) => GetAsync<List<TrackDto>>($"api/conferences/{conferenceId}/tracks");
    public Task<TrackDto> CreateTrackAsync(int conferenceId, TrackRequest r) => SendAsync<TrackDto>(HttpMethod.Post, $"api/conferences/{conferenceId}/tracks", r);
    public Task<TrackDto> UpdateTrackAsync(int conferenceId, int id, TrackRequest r) => SendAsync<TrackDto>(HttpMethod.Put, $"api/conferences/{conferenceId}/tracks/{id}", r);
    public Task DeleteTrackAsync(int conferenceId, int id) => DeleteAsync($"api/conferences/{conferenceId}/tracks/{id}");

    // Raeume
    public Task<List<RoomDto>> GetRoomsAsync(int conferenceId) => GetAsync<List<RoomDto>>($"api/conferences/{conferenceId}/rooms");
    public Task<RoomDto> CreateRoomAsync(int conferenceId, RoomRequest r) => SendAsync<RoomDto>(HttpMethod.Post, $"api/conferences/{conferenceId}/rooms", r);
    public Task<RoomDto> UpdateRoomAsync(int conferenceId, int id, RoomRequest r) => SendAsync<RoomDto>(HttpMethod.Put, $"api/conferences/{conferenceId}/rooms/{id}", r);
    public Task DeleteRoomAsync(int conferenceId, int id) => DeleteAsync($"api/conferences/{conferenceId}/rooms/{id}");

    // Sessions
    public Task<List<SessionDto>> GetSessionsAsync(int conferenceId) => GetAsync<List<SessionDto>>($"api/conferences/{conferenceId}/sessions");
    public Task<SessionDto> CreateSessionAsync(int conferenceId, SessionRequest r) => SendAsync<SessionDto>(HttpMethod.Post, $"api/conferences/{conferenceId}/sessions", r);
    public Task<SessionDto> UpdateSessionAsync(int conferenceId, int id, SessionRequest r) => SendAsync<SessionDto>(HttpMethod.Put, $"api/conferences/{conferenceId}/sessions/{id}", r);
    public Task DeleteSessionAsync(int conferenceId, int id) => DeleteAsync($"api/conferences/{conferenceId}/sessions/{id}");

    // Speaker
    public Task<List<SpeakerDto>> GetSpeakersAsync() => GetAsync<List<SpeakerDto>>("api/speakers");
    public Task<SpeakerDetailDto> GetSpeakerAsync(int id) => GetAsync<SpeakerDetailDto>($"api/speakers/{id}");
    public Task<SpeakerDto> CreateSpeakerAsync(SpeakerRequest r) => SendAsync<SpeakerDto>(HttpMethod.Post, "api/speakers", r);
    public Task<SpeakerDto> UpdateSpeakerAsync(int id, SpeakerRequest r) => SendAsync<SpeakerDto>(HttpMethod.Put, $"api/speakers/{id}", r);
    public Task DeleteSpeakerAsync(int id) => DeleteAsync($"api/speakers/{id}");

    // ---------- Helfer ----------

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await SafeAsync(() => http.GetAsync(url));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object body)
    {
        using var response = await SafeAsync(() => http.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) }));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task DeleteAsync(string url)
    {
        using var response = await SafeAsync(() => http.DeleteAsync(url));
        await EnsureSuccessAsync(response);
    }

    private static async Task<HttpResponseMessage> SafeAsync(Func<Task<HttpResponseMessage>> call)
    {
        try { return await call(); }
        // Netzwerkfehler und Timeouts: Server nicht erreichbar
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException(UnavailableMessage, HttpStatusCode.ServiceUnavailable);
        }
    }

    private const string UnavailableMessage = "Der Server ist gerade nicht erreichbar. Prüfe deine Internetverbindung und versuche es erneut.";

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        ApiProblem? problem = null;
        try { problem = await response.Content.ReadFromJsonAsync<ApiProblem>(); } catch { /* kein ProblemDetails-Body */ }

        // Fehlertext nach Ursache: Gateway/Netzwerk, Validierung, 404, Serverfehler
        var status = response.StatusCode;
        var message = problem switch
        {
            _ when status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => UnavailableMessage,
            _ when (int)status >= 500 => $"Auf dem Server ist ein Fehler aufgetreten (Fehler {(int)status}). Bitte versuche es später erneut.",
            { Errors.Count: > 0 } => string.Join(" ", problem.Errors.SelectMany(e => e.Value)),
            { Detail: { Length: > 0 } detail } => detail,
            _ when status == HttpStatusCode.NotFound => "Der angeforderte Inhalt wurde nicht gefunden.",
            { Title: { Length: > 0 } title } => title,
            _ => $"Fehler {(int)status}.",
        };
        throw new ApiException(message, status);
    }

    private sealed record ApiProblem(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
