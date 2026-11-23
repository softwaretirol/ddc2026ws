using ConferenceApp.Shared;

namespace ConferenceApp.Client.Components;

/// <summary>
/// Reine Zeitlogik für „Heute/Jetzt“ in der Agenda. Bewusst ohne Zugriff auf die Uhr:
/// Aufrufer übergeben Datum/Uhrzeit (aus einem <see cref="TimeProvider"/>), damit alles testbar bleibt.
/// </summary>
public static class AgendaNow
{
    /// <summary>
    /// Starttag der Agenda: ein explizit gewünschter Tag (z. B. später aus einem Query-Parameter) hat Vorrang,
    /// sofern er innerhalb der Konferenz liegt; sonst heute (falls Konferenztag); sonst der erste Tag.
    /// </summary>
    public static DateOnly InitialDay(ConferenceDto conference, DateOnly today, DateOnly? requested = null)
    {
        if (requested is { } r && IsConferenceDay(conference, r)) return r;
        return IsConferenceDay(conference, today) ? today : conference.StartDate;
    }

    public static bool IsConferenceDay(ConferenceDto conference, DateOnly day) =>
        day >= conference.StartDate && day <= conference.EndDate;

    public static bool IsRunning(SessionDto s, DateOnly today, TimeOnly now) =>
        s.Day == today && s.StartTime <= now && now < s.EndTime;

    /// <summary>Sessions, die heute gerade laufen, nach Beginn sortiert.</summary>
    public static List<SessionDto> Running(IEnumerable<SessionDto> sessions, DateOnly today, TimeOnly now) =>
        sessions.Where(s => IsRunning(s, today, now)).OrderBy(s => s.StartTime).ThenBy(s => s.RoomName).ToList();

    /// <summary>Alle Sessions des nächsten heutigen Beginn-Zeitpunkts (parallele Slots gemeinsam).</summary>
    public static List<SessionDto> Next(IEnumerable<SessionDto> sessions, DateOnly today, TimeOnly now)
    {
        var upcoming = sessions.Where(s => s.Day == today && s.StartTime > now).ToList();
        if (upcoming.Count == 0) return [];
        var first = upcoming.Min(s => s.StartTime);
        return upcoming.Where(s => s.StartTime == first).OrderBy(s => s.RoomName).ToList();
    }
}
