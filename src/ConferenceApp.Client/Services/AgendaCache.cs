using ConferenceApp.Shared;

namespace ConferenceApp.Client.Services;

/// <summary>
/// Hält die Agenda je Konferenz-Slug einige Minuten im Speicher, damit der Wechsel
/// Agenda → Session-Detail → Agenda nur einmal lädt. Scoped: in Blazor WASM lebt
/// der Cache so lange wie die App-Instanz im Browser-Tab.
/// </summary>
public sealed class AgendaCache : IDisposable
{
    /// <summary>Wie lange eine geladene Agenda gültig bleibt.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ApiClient _api;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public AgendaCache(ApiClient api, TimeProvider time)
    {
        _api = api;
        _time = time;
        // Schreibzugriffe (Admin) machen gecachte Agenden ungültig.
        _api.DataChanged += Clear;
    }

    /// <summary>
    /// Liefert die Agenda aus dem Cache oder lädt sie. Gleichzeitige Aufrufe für denselben
    /// Slug teilen sich einen Request; fehlgeschlagene Ladevorgänge werden nicht gecacht.
    /// </summary>
    public async Task<AgendaDto> GetAsync(string slug)
    {
        var now = _time.GetUtcNow();
        if (!_entries.TryGetValue(slug, out var entry) || entry.ExpiresAt <= now)
        {
            entry = new Entry(_api.GetAgendaAsync(slug), now + Lifetime);
            _entries[slug] = entry;
        }

        try
        {
            return await entry.Load;
        }
        catch
        {
            if (_entries.TryGetValue(slug, out var current) && ReferenceEquals(current, entry))
                _entries.Remove(slug);
            throw;
        }
    }

    public void Invalidate(string slug) => _entries.Remove(slug);

    public void Clear() => _entries.Clear();

    public void Dispose() => _api.DataChanged -= Clear;

    private sealed record Entry(Task<AgendaDto> Load, DateTimeOffset ExpiresAt);
}
