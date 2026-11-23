using System.Globalization;

namespace ConferenceApp.Client.Components;

/// <summary>
/// Kontrastberechnung nach WCAG 2.x (relative Luminanz, Kontrastverhältnis).
/// Track-Farben werden nur für Flächen und Ränder verwendet; Text darauf bekommt per <see cref="TextOn"/> Schwarz oder Weiß.
/// </summary>
public static class ColorContrast
{
    public const string Black = "#000000";
    public const string White = "#FFFFFF";

    /// <summary>Mindestkontrast für normalen Text (WCAG AA).</summary>
    public const double MinText = 4.5;

    /// <summary>Mindestkontrast für Grafiken und Bedienelemente wie Ränder und Punkte (WCAG AA, 1.4.11).</summary>
    public const double MinNonText = 3.0;

    /// <summary>Liest eine Farbe im Format #RRGGBB oder #RGB.</summary>
    public static bool TryParse(string? hex, out (byte R, byte G, byte B) rgb)
    {
        rgb = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s.Select(c => new string(c, 2)));
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        rgb = ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    /// <summary>Relative Luminanz (0 = Schwarz, 1 = Weiß); null bei ungültiger Farbe.</summary>
    public static double? Luminance(string? hex) =>
        TryParse(hex, out var c) ? 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B) : null;

    /// <summary>Kontrastverhältnis zweier Farben (1 bis 21); null, wenn eine Farbe ungültig ist.</summary>
    public static double? Ratio(string? a, string? b)
    {
        if (Luminance(a) is not { } la || Luminance(b) is not { } lb) return null;
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Textfarbe (Schwarz oder Weiß) mit dem höheren Kontrast auf der gegebenen Hintergrundfarbe.</summary>
    public static string TextOn(string? background) =>
        Luminance(background) is { } l && (l + 0.05) / 0.05 >= 1.05 / (l + 0.05) ? Black : White;

    /// <summary>Formatiert ein Kontrastverhältnis deutsch, z. B. „4,6:1“.</summary>
    public static string FormatRatio(double ratio) => ratio.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")) + ":1";

    private static double Channel(byte v)
    {
        var c = v / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
