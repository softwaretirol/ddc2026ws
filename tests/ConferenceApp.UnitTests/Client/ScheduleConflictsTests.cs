using ConferenceApp.Client.Components;
using ConferenceApp.Shared;
using static ConferenceApp.UnitTests.Client.TestData;

namespace ConferenceApp.UnitTests.Client;

/// <summary>#15: Raum belegt blockiert (wie die API), Speaker parallel eingeplant warnt nur; Beginn = Ende der letzten Session.</summary>
public class ScheduleConflictsTests
{
    private static readonly SpeakerSummaryDto Anna = Speaker(1, "Anna");
    private static readonly SpeakerSummaryDto Ben = Speaker(2, "Ben");

    private static readonly SessionDto[] Existing =
    [
        Session(10, "09:00", "10:00", roomId: 1, title: "Keynote", speakers: Anna),
        Session(11, "10:30", "11:15", roomId: 2, title: "Talk B", speakers: Ben),
    ];

    private static SessionRequest Req(string start, string end, int roomId = 1, DateOnly? day = null, params int[] speakers) => new()
    {
        Title = "Neu", Day = day ?? Day1, StartTime = TimeOnly.Parse(start), EndTime = TimeOnly.Parse(end), RoomId = roomId, SpeakerIds = [.. speakers],
    };

    [Theory]
    [InlineData("09:30", "10:30")]
    [InlineData("08:30", "09:01")]
    [InlineData("09:00", "10:00")]
    [InlineData("08:00", "11:00")]
    public void Ueberschneidung_im_selben_Raum_blockiert(string start, string end) =>
        Assert.Equal([10], ScheduleConflicts.RoomConflicts(Existing, Req(start, end), null).Select(s => s.Id));

    [Theory]
    [InlineData("10:00", "11:00")]   // beginnt genau zum Ende
    [InlineData("08:00", "09:00")]   // endet genau zum Beginn
    public void Ende_gleich_Beginn_ist_keine_Ueberschneidung(string start, string end) =>
        Assert.Empty(ScheduleConflicts.RoomConflicts(Existing, Req(start, end), null));

    [Fact]
    public void Anderer_Raum_oder_anderer_Tag_ist_kein_Raumkonflikt()
    {
        Assert.Empty(ScheduleConflicts.RoomConflicts(Existing, Req("09:00", "10:00", roomId: 3), null));
        Assert.Empty(ScheduleConflicts.RoomConflicts(Existing, Req("09:00", "10:00", day: Day2), null));
    }

    [Fact]
    public void Bearbeitete_Session_kollidiert_nicht_mit_sich_selbst() =>
        Assert.Empty(ScheduleConflicts.RoomConflicts(Existing, Req("09:00", "10:00"), excludeId: 10));

    [Fact]
    public void Speaker_parallel_in_anderem_Raum_wird_gemeldet()
    {
        var conflicts = ScheduleConflicts.SpeakerConflicts(Existing, Req("10:45", "11:30", roomId: 3, speakers: [2]), null);
        var (speaker, session) = Assert.Single(conflicts);
        Assert.Equal(("Ben", 11), (speaker.Name, session.Id));
    }

    [Fact]
    public void Speaker_ohne_Ueberschneidung_oder_nicht_gewaehlt_meldet_nichts()
    {
        Assert.Empty(ScheduleConflicts.SpeakerConflicts(Existing, Req("11:15", "12:00", roomId: 3, speakers: [2]), null));
        Assert.Empty(ScheduleConflicts.SpeakerConflicts(Existing, Req("10:45", "11:30", roomId: 3, speakers: [1]), null));
        Assert.Empty(ScheduleConflicts.SpeakerConflicts(Existing, Req("10:45", "11:30", roomId: 3), null));
    }

    [Fact]
    public void Vorschlag_beginnt_mit_Ende_der_letzten_Session_im_Raum()
    {
        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(11, 0)), ScheduleConflicts.SuggestSlot(Existing, Day1, 1));
        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(10, 0)), ScheduleConflicts.SuggestSlot(Existing, Day1, 3));
        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(9, 45)), ScheduleConflicts.SuggestSlot(Existing, Day2, 1, 45));
    }

    [Fact]
    public void Naechste_freie_Zeit_gleicher_Dauer()
    {
        var sessions = new[] { Session(1, "09:00", "10:00"), Session(2, "10:30", "12:00") };
        Assert.Equal((new TimeOnly(12, 0), new TimeOnly(13, 0)), ScheduleConflicts.NextFreeSlot(sessions, Day1, 1, null, new TimeOnly(9, 30), 60));
        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(10, 30)), ScheduleConflicts.NextFreeSlot(sessions, Day1, 1, null, new TimeOnly(9, 30), 30));
        Assert.Null(ScheduleConflicts.NextFreeSlot([Session(1, "00:00", "23:59")], Day1, 1, null, new TimeOnly(0, 0), 60));
    }

    [Theory]
    [InlineData("9", 9, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("0930", 9, 30)]
    [InlineData("9:30", 9, 30)]
    [InlineData("09:30", 9, 30)]
    [InlineData("9.30", 9, 30)]
    [InlineData(" 13:05 ", 13, 5)]
    public void Uhrzeit_eingeben(string text, int h, int m)
    {
        Assert.True(ScheduleConflicts.TryParseTime(text, out var t));
        Assert.Equal(new TimeOnly(h, m), t);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("24:00")]
    [InlineData("960")]
    [InlineData("abc")]
    [InlineData("12345")]
    public void Ungueltige_Uhrzeit(string? text) => Assert.False(ScheduleConflicts.TryParseTime(text, out _));
}
