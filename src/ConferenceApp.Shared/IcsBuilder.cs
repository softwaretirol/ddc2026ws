using System.Globalization;
using System.Text;

namespace ConferenceApp.Shared;

/// <summary>
/// Erzeugt iCalendar-Dateien (RFC 5545) für Sessions. Wird vom Client (Einzel-Session)
/// und von der API (abonnierbare Konferenz-Agenda) gemeinsam genutzt.
/// Zeiten sind bewusst "floating" (ohne Z/TZID): Sie gelten in der Ortszeit der Konferenz.
/// </summary>
public static class IcsBuilder
{
    public const string ContentType = "text/calendar";

    private const string Crlf = "\r\n";
    private const int MaxLineOctets = 75;

    /// <summary>Baut einen VCALENDAR mit einem VEVENT pro Session.</summary>
    /// <param name="calendarName">Anzeigename des Kalenders (X-WR-CALNAME).</param>
    /// <param name="conference">Konferenz der Sessions (für Ort und Beschreibung).</param>
    /// <param name="sessions">Die zu exportierenden Sessions.</param>
    /// <param name="sessionUrl">Optional: absolute URL zur Session-Detailseite.</param>
    /// <param name="timestamp">DTSTAMP; Standard ist die aktuelle UTC-Zeit.</param>
    public static string Build(
        string calendarName,
        ConferenceDto conference,
        IEnumerable<SessionDto> sessions,
        Func<SessionDto, string?>? sessionUrl = null,
        DateTime? timestamp = null)
    {
        var stamp = FormatUtc(timestamp ?? DateTime.UtcNow);
        var sb = new StringBuilder();

        Line(sb, "BEGIN:VCALENDAR");
        Line(sb, "VERSION:2.0");
        Line(sb, "PRODID:-//ConferenceApp//Agenda//DE");
        Line(sb, "CALSCALE:GREGORIAN");
        Line(sb, "METHOD:PUBLISH");
        Line(sb, "X-WR-CALNAME:" + Escape(calendarName));

        foreach (var s in sessions)
        {
            Line(sb, "BEGIN:VEVENT");
            Line(sb, $"UID:session-{s.Id}@conferenceapp");
            Line(sb, "DTSTAMP:" + stamp);
            Line(sb, "DTSTART:" + FormatFloating(s.Day, s.StartTime));
            Line(sb, "DTEND:" + FormatFloating(s.Day, s.EndTime));
            Line(sb, "SUMMARY:" + Escape(s.Title));
            Line(sb, "LOCATION:" + Escape(Location(s, conference)));
            Line(sb, "DESCRIPTION:" + Escape(Description(s, conference)));
            if (s.TrackName is { Length: > 0 } track)
                Line(sb, "CATEGORIES:" + Escape(track));
            if (sessionUrl?.Invoke(s) is { Length: > 0 } url)
                Line(sb, "URL:" + url);
            Line(sb, "END:VEVENT");
        }

        Line(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>Dateiname-tauglicher Bezeichner (Kleinbuchstaben, Ziffern, Bindestriche).</summary>
    public static string FileName(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
            else if (ch switch { 'ä' => "ae", 'ö' => "oe", 'ü' => "ue", 'ß' => "ss", _ => null } is { } umlaut) sb.Append(umlaut);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > 60) slug = slug[..60].TrimEnd('-');
        return (slug.Length > 0 ? slug : "kalender") + ".ics";
    }

    private static string Location(SessionDto s, ConferenceDto c) =>
        string.IsNullOrWhiteSpace(c.Location) ? s.RoomName : $"{s.RoomName}, {c.Location}";

    private static string Description(SessionDto s, ConferenceDto c)
    {
        var parts = new List<string>();
        if (s.Speakers.Count > 0)
            parts.Add("Speaker: " + string.Join(", ", s.Speakers.Select(sp => sp.Name)));
        if (s.TrackName is { Length: > 0 })
            parts.Add("Track: " + s.TrackName);
        if (!string.IsNullOrWhiteSpace(s.Abstract))
            parts.Add(s.Abstract.Trim());
        parts.Add(c.Name);
        return string.Join("\n\n", parts);
    }

    private static string FormatFloating(DateOnly day, TimeOnly time) =>
        day.ToDateTime(time).ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string FormatUtc(DateTime value) =>
        (value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value)
            .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>TEXT-Escaping nach RFC 5545, 3.3.11.</summary>
    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            switch (ch)
            {
                case '\\': sb.Append(@"\\"); break;
                case ';': sb.Append(@"\;"); break;
                case ',': sb.Append(@"\,"); break;
                case '\r':
                    if (i + 1 < value.Length && value[i + 1] == '\n') i++;
                    sb.Append(@"\n");
                    break;
                case '\n': sb.Append(@"\n"); break;
                default:
                    if (!char.IsControl(ch) || ch == '\t') sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Schreibt eine Inhaltszeile mit Zeilenfaltung nach 75 Oktetten (RFC 5545, 3.1).
    /// Gefaltet wird nur zwischen Zeichen, damit UTF-8-Sequenzen nicht zerrissen werden.
    /// </summary>
    private static void Line(StringBuilder sb, string line)
    {
        var octets = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var len = char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]) ? 2 : 1;
            var bytes = Encoding.UTF8.GetByteCount(line.AsSpan(i, len));
            if (octets + bytes > MaxLineOctets)
            {
                sb.Append(Crlf).Append(' ');
                octets = 1; // das führende Leerzeichen zählt mit
            }
            sb.Append(line, i, len);
            octets += bytes;
            i += len - 1;
        }
        sb.Append(Crlf);
    }
}
