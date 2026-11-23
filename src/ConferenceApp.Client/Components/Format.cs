using System.Globalization;

namespace ConferenceApp.Client.Components;

/// <summary>Einheitliche deutsche Datums-/Zeitformatierung.</summary>
public static class Format
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static string Day(DateOnly d) => d.ToString("ddd, dd.MM.yyyy", De);
    public static string Date(DateOnly d) => d.ToString("dd.MM.yyyy", De);
    public static string Time(TimeOnly t) => t.ToString("HH:mm", De);
    public static string Range(TimeOnly a, TimeOnly b) => $"{Time(a)}–{Time(b)}";

    public static string DateRange(DateOnly start, DateOnly end) =>
        start == end ? Date(start) : $"{start.ToString("dd.MM.", De)}–{Date(end)}";

    public static IEnumerable<DateOnly> Days(DateOnly start, DateOnly end)
    {
        for (var d = start; d <= end; d = d.AddDays(1)) yield return d;
    }
}
