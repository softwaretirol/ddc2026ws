using ConferenceApp.Client.Components;
using static ConferenceApp.UnitTests.Client.TestData;

namespace ConferenceApp.UnitTests.Client;

/// <summary>#6: Starttag = Query-Parameter, sonst heute (falls Konferenztag), sonst Tag 1; „Läuft gerade / Als Nächstes“.</summary>
public class AgendaNowTests
{
    private static readonly DateOnly Before = Day1.AddDays(-10);

    [Fact]
    public void Ohne_Query_vor_der_Konferenz_startet_mit_Tag_1() =>
        Assert.Equal(Day1, AgendaNow.InitialDay(Conference(), Before));

    [Fact]
    public void Am_zweiten_Konferenztag_startet_mit_heute() =>
        Assert.Equal(Day2, AgendaNow.InitialDay(Conference(), Day2));

    [Fact]
    public void Nach_der_Konferenz_startet_mit_Tag_1() =>
        Assert.Equal(Day1, AgendaNow.InitialDay(Conference(), Day2.AddDays(1)));

    [Fact]
    public void Query_hat_Vorrang_vor_heute() =>
        Assert.Equal(Day1, AgendaNow.InitialDay(Conference(), Day2, requested: Day1));

    [Fact]
    public void Query_ausserhalb_der_Konferenz_wird_ignoriert()
    {
        Assert.Equal(Day2, AgendaNow.InitialDay(Conference(), Day2, requested: Day2.AddDays(1)));
        Assert.Equal(Day1, AgendaNow.InitialDay(Conference(), Before, requested: Day1.AddDays(-1)));
    }

    [Fact]
    public void Laeuft_gerade_Beginn_inklusive_Ende_exklusiv()
    {
        var s = Session(1, "10:00", "11:00");
        Assert.True(AgendaNow.IsRunning(s, Day1, new TimeOnly(10, 0)));
        Assert.True(AgendaNow.IsRunning(s, Day1, new TimeOnly(10, 59)));
        Assert.False(AgendaNow.IsRunning(s, Day1, new TimeOnly(11, 0)));
        Assert.False(AgendaNow.IsRunning(s, Day2, new TimeOnly(10, 30)));
    }

    [Fact]
    public void Als_Naechstes_liefert_alle_parallelen_Sessions_des_naechsten_Beginns()
    {
        var sessions = new[]
        {
            Session(1, "09:00", "10:00"),
            Session(2, "10:30", "11:15", 2, roomName: "Raum B"),
            Session(3, "10:30", "11:15", 1, roomName: "Raum A"),
            Session(4, "13:00", "13:45"),
            Session(5, "10:15", "11:00", day: Day2),
        };

        var next = AgendaNow.Next(sessions, Day1, new TimeOnly(9, 30));

        Assert.Equal([3, 2], next.Select(s => s.Id));
        Assert.Equal([1], AgendaNow.Running(sessions, Day1, new TimeOnly(9, 30)).Select(s => s.Id));
        Assert.Empty(AgendaNow.Next(sessions, Day1, new TimeOnly(13, 0)));
    }
}
