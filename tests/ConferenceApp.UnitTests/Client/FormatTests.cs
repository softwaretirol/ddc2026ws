using ConferenceApp.Client.Components;

namespace ConferenceApp.UnitTests.Client;

public class FormatTests
{
    [Fact]
    public void Datum_im_deutschen_Format() => Assert.Equal("01.12.2026", Format.Date(new DateOnly(2026, 12, 1)));

    [Fact]
    public void Tag_mit_deutschem_Wochentag()
    {
        // 01.12.2026 ist ein Dienstag. Die Abkürzung ("Di" bzw. "Di.") hängt von den ICU-Daten ab.
        var text = Format.Day(new DateOnly(2026, 12, 1));
        Assert.StartsWith("Di", text);
        Assert.EndsWith(", 01.12.2026", text);
    }

    [Theory]
    [InlineData(9, 0, "09:00")]
    [InlineData(13, 5, "13:05")]
    [InlineData(0, 0, "00:00")]
    [InlineData(23, 59, "23:59")]
    public void Uhrzeit_24h(int h, int m, string expected) => Assert.Equal(expected, Format.Time(new TimeOnly(h, m)));

    [Fact]
    public void Zeitspanne_mit_Halbgeviertstrich() =>
        Assert.Equal("09:00–10:30", Format.Range(new TimeOnly(9, 0), new TimeOnly(10, 30)));

    [Fact]
    public void Datumsbereich_eintaegig_zeigt_nur_ein_Datum() =>
        Assert.Equal("01.12.2026", Format.DateRange(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 1)));

    [Fact]
    public void Datumsbereich_mehrtaegig() =>
        Assert.Equal("01.12.–02.12.2026", Format.DateRange(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2)));

    [Fact]
    public void Datumsbereich_ueber_Monatsgrenze() =>
        Assert.Equal("30.11.–01.12.2026", Format.DateRange(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 1)));

    [Fact]
    public void Tage_inklusive_Start_und_Ende()
    {
        var days = Format.Days(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 2)).ToList();
        Assert.Equal([new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2)], days);
    }

    [Fact]
    public void Tage_eintaegig() =>
        Assert.Equal([new DateOnly(2026, 12, 1)], Format.Days(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 1)));

    [Fact]
    public void Tage_bei_Ende_vor_Start_leer() =>
        Assert.Empty(Format.Days(new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 1)));
}
