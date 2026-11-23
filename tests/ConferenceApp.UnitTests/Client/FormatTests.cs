using ConferenceApp.Client.Components;

namespace ConferenceApp.UnitTests.Client;

public class FormatTests
{
    [Fact]
    public void Datum_im_deutschen_Format() => Assert.Equal("01.12.2026", Format.Date(new DateOnly(2026, 12, 1)));

    // #18: ausführlich „Di, 01.12.2026“, kompakt „Di 01.12.“ – einheitlich ohne Punkt nach dem Wochentag (unabhängig von ICU).
    [Fact]
    public void Tag_mit_deutschem_Wochentag() => Assert.Equal("Di, 01.12.2026", Format.Day(new DateOnly(2026, 12, 1)));

    [Fact]
    public void Tag_kompakt_fuer_Tabs_und_Listen() => Assert.Equal("Mi 02.12.", Format.DayShort(new DateOnly(2026, 12, 2)));

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

    // #18: „DateRange im selben Monat kompakt“
    [Fact]
    public void Datumsbereich_mehrtaegig_im_selben_Monat_kompakt() =>
        Assert.Equal("01.–02.12.2026", Format.DateRange(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2)));

    // Über den Jahreswechsel muss das Startjahr sichtbar sein (früher offene Frage).
    [Fact]
    public void Datumsbereich_ueber_Jahreswechsel_zeigt_beide_Jahre() =>
        Assert.Equal("28.12.2026–02.01.2027", Format.DateRange(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 2)));

    [Theory]
    [InlineData(1, "1 Session")]
    [InlineData(0, "0 Sessions")]
    [InlineData(4, "4 Sessions")]
    public void Anzahl_mit_Einzahl_und_Mehrzahl(int n, string expected) => Assert.Equal(expected, Format.Count(n, "Session", "Sessions"));

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
