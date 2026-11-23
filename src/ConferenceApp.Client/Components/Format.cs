using System.Globalization;

namespace ConferenceApp.Client.Components;

/// <summary>Einheitliche deutsche Datums-/Zeitformatierung.</summary>
public static class Format
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ausführlich, z. B. „Di, 01.12.2026“ – für Detailseiten.</summary>
    public static string Day(DateOnly d) => $"{DayName(d)}, {Date(d)}";

    /// <summary>Kompakt, z. B. „Di 01.12.“ – für Tagesbuttons, Tabellen und Listen.</summary>
    public static string DayShort(DateOnly d) => $"{DayName(d)} {d.ToString("dd.MM.", De)}";

    public static string Date(DateOnly d) => d.ToString("dd.MM.yyyy", De);
    public static string Time(TimeOnly t) => t.ToString("HH:mm", De);
    public static string Range(TimeOnly a, TimeOnly b) => $"{Time(a)}–{Time(b)}";

    /// <summary>
    /// Zeitraum so kompakt wie möglich: „01.12.2026“, „01.–03.12.2026“ (gleicher Monat),
    /// „30.11.–02.12.2026“ (gleiches Jahr), sonst beide Daten vollständig.
    /// </summary>
    public static string DateRange(DateOnly start, DateOnly end)
    {
        if (start == end) return Date(start);
        if (start.Year != end.Year) return $"{Date(start)}–{Date(end)}";
        if (start.Month != end.Month) return $"{start.ToString("dd.MM.", De)}–{Date(end)}";
        return $"{start.ToString("dd.", De)}–{Date(end)}";
    }

    public static IEnumerable<DateOnly> Days(DateOnly start, DateOnly end)
    {
        for (var d = start; d <= end; d = d.AddDays(1)) yield return d;
    }

    // Je nach ICU-Daten liefert de-DE „Di“ oder „Di.“ – einheitlich ohne Punkt.
    private static string DayName(DateOnly d) =>
        De.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).TrimEnd('.');
}
