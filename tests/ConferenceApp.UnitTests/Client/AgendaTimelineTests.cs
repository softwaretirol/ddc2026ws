using ConferenceApp.Client.Components;
using static ConferenceApp.UnitTests.Client.TestData;

namespace ConferenceApp.UnitTests.Client;

/// <summary>#2: Zeiträume ab 30 Minuten ohne Session in allen Räumen sind Pausen („Pause · 11:15–13:00“).</summary>
public class AgendaTimelineTests
{
    [Fact]
    public void Luecke_aus_dem_Seed_wird_als_Pause_erkannt_und_beschriftet()
    {
        // Seed Tag 1: 09:00–10:00, drei parallele 10:30–11:15, zwei parallele 13:00–13:45
        var t = AgendaTimeline.Build([
            Session(1, "09:00", "10:00", 1),
            Session(2, "10:30", "11:15", 1), Session(3, "10:30", "11:15", 2), Session(4, "10:30", "11:15", 3),
            Session(5, "13:00", "13:45", 1), Session(6, "13:00", "13:45", 2),
        ]);

        // 10:00–10:30 ist genau 30 Minuten lang und damit ebenfalls eine Pause.
        Assert.Equal(["Pause · 10:00–10:30", "Pause · 11:15–13:00"], t.Gaps.Select(g => g.Label));
    }

    [Fact]
    public void Luecke_von_genau_30_Minuten_ist_eine_Pause()
    {
        var t = AgendaTimeline.Build([Session(1, "09:00", "10:00"), Session(2, "10:30", "11:00")]);
        var gap = Assert.Single(t.Gaps);
        Assert.Equal(new TimeOnly(10, 0), gap.Start);
        Assert.Equal(TimeSpan.FromMinutes(30), gap.Duration);
    }

    [Fact]
    public void Luecke_unter_30_Minuten_ist_keine_Pause()
    {
        var t = AgendaTimeline.Build([Session(1, "09:00", "10:00"), Session(2, "10:29", "11:00")]);
        Assert.Empty(t.Gaps);
        Assert.Single(t.Segments);
    }

    [Fact]
    public void Lauft_in_einem_anderen_Raum_etwas_ist_es_keine_Pause()
    {
        // Raum 1 hat 10:00–12:00 frei, Raum 2 ist 10:00–11:30 belegt -> Pause nur 11:30–12:00
        var t = AgendaTimeline.Build([
            Session(1, "09:00", "10:00", 1), Session(2, "12:00", "13:00", 1),
            Session(3, "10:00", "11:30", 2),
        ]);
        var gap = Assert.Single(t.Gaps);
        Assert.Equal((new TimeOnly(11, 30), new TimeOnly(12, 0)), (gap.Start, gap.End));
    }

    [Fact]
    public void Segmente_sind_lueckenlos_und_decken_den_Tag_ab()
    {
        var t = AgendaTimeline.Build([Session(1, "09:00", "10:00"), Session(2, "11:00", "12:00"), Session(3, "14:00", "15:00")]);

        Assert.Equal(new TimeOnly(9, 0), t.Start);
        Assert.Equal(new TimeOnly(15, 0), t.End);
        for (var i = 1; i < t.Segments.Count; i++)
            Assert.Equal(t.Segments[i - 1].End, t.Segments[i].Start);
        Assert.Equal(2, t.Gaps.Count());
    }

    [Fact]
    public void Erweiterter_Rand_zaehlt_nie_als_Pause()
    {
        var t = AgendaTimeline.Build([Session(1, "09:30", "10:00")], from: new TimeOnly(8, 0), to: new TimeOnly(12, 0));
        Assert.Empty(t.Gaps);
        Assert.Equal((new TimeOnly(8, 0), new TimeOnly(12, 0)), (t.Start, t.End));
    }

    [Fact]
    public void Zeitpunkt_in_Pause_und_Ende_exklusiv()
    {
        var t = AgendaTimeline.Build([Session(1, "09:00", "10:00"), Session(2, "11:00", "12:00")]);
        Assert.True(t.IsInGap(new TimeOnly(10, 0)));
        Assert.True(t.IsInGap(new TimeOnly(10, 59)));
        Assert.False(t.IsInGap(new TimeOnly(11, 0)));
        Assert.Null(t.SegmentAt(new TimeOnly(12, 0)));
    }

    [Fact]
    public void Ohne_Sessions_leer() => Assert.Empty(AgendaTimeline.Build([]).Segments);
}
