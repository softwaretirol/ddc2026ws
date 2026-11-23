using System.Globalization;
using ConferenceApp.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace ConferenceApp.Client.Components;

/// <summary>
/// Konfliktprüfung und Zeitvorschläge für den Session-Dialog auf Basis der bereits geladenen Sessions.
/// Die Raumregel entspricht <c>SessionEndpoints.ApplyAsync</c> (halboffene Intervalle: Ende 10:00 und Beginn 10:00 kollidieren nicht).
/// </summary>
public static class ScheduleConflicts
{
    public static readonly TimeOnly DefaultStart = new(9, 0);
    public const int DefaultDurationMinutes = 60;

    private static bool Overlaps(SessionDto s, DateOnly day, TimeOnly start, TimeOnly end) =>
        s.Day == day && s.StartTime < end && start < s.EndTime;

    /// <summary>Sessions im selben Raum, die sich mit dem Zeitraum überschneiden (blockierend, wie die API).</summary>
    public static List<SessionDto> RoomConflicts(IEnumerable<SessionDto> sessions, SessionRequest req, int? excludeId) =>
        sessions.Where(s => s.Id != excludeId && s.RoomId == req.RoomId && Overlaps(s, req.Day, req.StartTime, req.EndTime))
            .OrderBy(s => s.StartTime).ToList();

    /// <summary>Gewählte Speaker, die zur selben Zeit in einer anderen Session eingeplant sind (nur Warnung).</summary>
    public static List<(SpeakerSummaryDto Speaker, SessionDto Session)> SpeakerConflicts(IEnumerable<SessionDto> sessions, SessionRequest req, int? excludeId)
    {
        if (req.SpeakerIds.Count == 0) return [];
        return sessions
            .Where(s => s.Id != excludeId && Overlaps(s, req.Day, req.StartTime, req.EndTime))
            .SelectMany(s => s.Speakers.Where(sp => req.SpeakerIds.Contains(sp.Id)).Select(sp => (sp, s)))
            .OrderBy(x => x.sp.Name).ThenBy(x => x.s.StartTime)
            .ToList();
    }

    /// <summary>Sessions eines Raums an einem Tag, sortiert (für die Mini-Zeitleiste).</summary>
    public static List<SessionDto> RoomDay(IEnumerable<SessionDto> sessions, DateOnly day, int roomId, int? excludeId) =>
        sessions.Where(s => s.Id != excludeId && s.RoomId == roomId && s.Day == day).OrderBy(s => s.StartTime).ToList();

    /// <summary>Vorschlag für eine neue Session: Beginn = Ende der letzten Session im Raum an diesem Tag, sonst 09:00.</summary>
    public static (TimeOnly Start, TimeOnly End) SuggestSlot(IEnumerable<SessionDto> sessions, DateOnly day, int roomId, int durationMinutes = DefaultDurationMinutes)
    {
        var last = RoomDay(sessions, day, roomId, null).OrderBy(s => s.EndTime).LastOrDefault();
        var start = last?.EndTime ?? DefaultStart;
        return (start, AddClamped(start, durationMinutes));
    }

    /// <summary>
    /// Nächster freier Zeitraum gleicher Dauer im Raum ab <paramref name="from"/>; null, wenn der Tag dafür nicht mehr reicht.
    /// </summary>
    public static (TimeOnly Start, TimeOnly End)? NextFreeSlot(IEnumerable<SessionDto> sessions, DateOnly day, int roomId, int? excludeId, TimeOnly from, int durationMinutes)
    {
        if (durationMinutes <= 0) return null;
        var busy = RoomDay(sessions, day, roomId, excludeId);
        var start = from;
        while (true)
        {
            var end = start.AddMinutes(durationMinutes);
            if (end <= start) return null; // über Mitternacht hinaus
            var clash = busy.Where(s => s.StartTime < end && start < s.EndTime).OrderBy(s => s.EndTime).LastOrDefault();
            if (clash is null) return (start, end);
            start = clash.EndTime;
        }
    }

    /// <summary>Addiert Minuten, ohne über Mitternacht hinauszulaufen.</summary>
    public static TimeOnly AddClamped(TimeOnly t, int minutes)
    {
        var end = t.AddMinutes(minutes);
        return end <= t ? new TimeOnly(23, 59) : end;
    }

    /// <summary>Liest eine Uhrzeit wie „9“, „930“, „0930“, „9:30“ oder „9.30“.</summary>
    public static bool TryParseTime(string? text, out TimeOnly time)
    {
        time = default;
        var s = (text ?? "").Trim().Replace('.', ':');
        if (s.Length == 0) return false;

        // „24:00“ liest .NET als 00:00 – im Format HH:mm ist das keine gültige Uhrzeit.
        if (s.Contains(':'))
            return !s.StartsWith("24:", StringComparison.Ordinal)
                && TimeOnly.TryParseExact(s, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

        if (!s.All(char.IsAsciiDigit) || s.Length > 4) return false;
        var value = int.Parse(s, CultureInfo.InvariantCulture);
        var (h, m) = s.Length <= 2 ? (value, 0) : (value / 100, value % 100);
        if (h > 23 || m > 59) return false;
        time = new TimeOnly(h, m);
        return true;
    }
}

/// <summary>
/// Hängt zusätzliche, blockierende Prüfungen an das umgebende <see cref="EditContext"/> (z. B. Raumkonflikt),
/// ohne den gemeinsamen <c>FormDialog</c> zu verändern. Meldungen werden im Dialog selbst angezeigt.
/// </summary>
public sealed class BlockingValidator : ComponentBase, IDisposable
{
    [CascadingParameter] private EditContext? EditContext { get; set; }

    /// <summary>Liefert die aktuell blockierenden Fehler; leer = speichern erlaubt.</summary>
    [Parameter, EditorRequired] public Func<IEnumerable<string>> Errors { get; set; } = () => [];

    private EditContext? _context;
    private ValidationMessageStore? _store;

    protected override void OnParametersSet()
    {
        if (EditContext == _context) return;
        if (_context is not null) _context.OnValidationRequested -= Validate;
        _context = EditContext ?? throw new InvalidOperationException($"{nameof(BlockingValidator)} benötigt ein umgebendes EditForm.");
        _store = new ValidationMessageStore(_context);
        _context.OnValidationRequested += Validate;
    }

    private void Validate(object? sender, ValidationRequestedEventArgs e)
    {
        _store!.Clear();
        var field = new FieldIdentifier(_context!.Model, nameof(BlockingValidator));
        foreach (var error in Errors()) _store.Add(field, error);
        _context.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (_context is not null) _context.OnValidationRequested -= Validate;
    }
}
