using System.Text;
using ConferenceApp.Shared;
using static ConferenceApp.UnitTests.Client.TestData;

namespace ConferenceApp.UnitTests.Shared;

/// <summary>#10 / RFC 5545: CRLF, TEXT-Escaping, Faltung nach 75 Oktetten, Zeiten „floating“ (ohne Z/TZID).</summary>
public class IcsBuilderTests
{
    private static readonly DateTime Stamp = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    private static string Build(params SessionDto[] sessions) =>
        IcsBuilder.Build("Dev Conference 2026", Conference(), sessions, timestamp: Stamp);

    /// <summary>Entfaltet gefaltete Zeilen (CRLF + Leerzeichen) nach RFC 5545, 3.1.</summary>
    private static List<string> Unfold(string ics) =>
        ics.Replace("\r\n ", "").Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();

    [Fact]
    public void Grundgeruest_mit_CRLF()
    {
        var ics = Build(Session(1, "09:00", "10:00"));

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));   // kein nacktes LF
        var lines = Unfold(ics);
        Assert.Contains("VERSION:2.0", lines);
        Assert.Contains(lines, l => l.StartsWith("PRODID:"));
        Assert.Contains("X-WR-CALNAME:Dev Conference 2026", lines);
        Assert.Equal(1, lines.Count(l => l == "BEGIN:VEVENT"));
        Assert.Equal(1, lines.Count(l => l == "END:VEVENT"));
    }

    [Fact]
    public void Zeiten_sind_floating_ohne_Zeitzone()
    {
        var lines = Unfold(Build(Session(1, "09:00", "10:30")));

        Assert.Contains("DTSTART:20261201T090000", lines);
        Assert.Contains("DTEND:20261201T103000", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("DTSTART") && (l.EndsWith('Z') || l.Contains("TZID")));
        Assert.Contains("DTSTAMP:20261123T100000Z", lines);
    }

    [Fact]
    public void Eindeutige_UID_pro_Session()
    {
        var lines = Unfold(Build(Session(1, "09:00", "10:00"), Session(2, "10:00", "11:00")));
        var uids = lines.Where(l => l.StartsWith("UID:")).ToList();
        Assert.Equal(2, uids.Distinct().Count());
    }

    [Fact]
    public void Text_wird_nach_RFC_5545_maskiert()
    {
        var lines = Unfold(Build(Session(1, "09:00", "10:00", title: @"APIs; Tipps, Tricks \ mehr", @abstract: "Zeile 1\r\nZeile 2\nZeile 3")));

        Assert.Contains(@"SUMMARY:APIs\; Tipps\, Tricks \\ mehr", lines);
        var description = Assert.Single(lines, l => l.StartsWith("DESCRIPTION:"));
        Assert.Contains(@"Zeile 1\nZeile 2\nZeile 3", description);
    }

    [Fact]
    public void Ort_enthaelt_Raum_und_Konferenzort()
    {
        var lines = Unfold(Build(Session(1, "09:00", "10:00", roomName: "Saal Tirol")));
        Assert.Contains(@"LOCATION:Saal Tirol\, Innsbruck", lines);
    }

    [Fact]
    public void Lange_Zeilen_werden_nach_75_Oktetten_gefaltet_ohne_UTF8_zu_zerreissen()
    {
        var title = string.Concat(Enumerable.Repeat("Größenänderung über Grenzen 🚀 ", 8));
        var ics = Build(Session(1, "09:00", "10:00", title: title));

        foreach (var physical in ics.Split("\r\n"))
            Assert.True(Encoding.UTF8.GetByteCount(physical) <= 75, $"{Encoding.UTF8.GetByteCount(physical)} Oktette: {physical}");

        // Entfaltet ergibt sich wieder der vollständige Titel; kein Ersatzzeichen durch zerrissene Sequenzen.
        Assert.Contains("SUMMARY:" + title.Replace(",", @"\,"), Unfold(ics));
        Assert.DoesNotContain('�', ics);
    }

    [Fact]
    public void Speaker_und_Track_landen_in_der_Beschreibung()
    {
        var lines = Unfold(Build(Session(1, "09:00", "10:00", track: ".NET", speakers: [Speaker(1, "Anna Berger"), Speaker(2, "David Novak")])));
        var description = Assert.Single(lines, l => l.StartsWith("DESCRIPTION:"));
        Assert.Contains(@"Anna Berger\, David Novak", description);
        Assert.Contains("CATEGORIES:.NET", lines);
    }

    [Fact]
    public void Ohne_Sessions_gueltiger_leerer_Kalender()
    {
        var lines = Unfold(Build());
        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.DoesNotContain("BEGIN:VEVENT", lines);
    }

    [Theory]
    [InlineData("devconf-2026", "devconf-2026.ics")]
    [InlineData("devconf-2026-Web & Frontend", "devconf-2026-web-frontend.ics")]
    [InlineData("Größe", "groesse.ics")]
    [InlineData("!!!", "kalender.ics")]
    public void Dateiname(string name, string expected) => Assert.Equal(expected, IcsBuilder.FileName(name));
}
