using ConferenceApp.Shared;
using static ConferenceApp.UnitTests.Validation.ValidationHelper;

namespace ConferenceApp.UnitTests.Validation;

public class SessionRequestTests
{
    private static SessionRequest Valid() => new()
    {
        Title = "Was ist neu in .NET 11",
        Abstract = "Abstract",
        Day = new DateOnly(2027, 11, 22),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(9, 45),
        RoomId = 1,
        TrackId = null,
        SpeakerIds = [1, 2],
    };

    [Fact]
    public void Gueltige_Session_hat_keine_Fehler() => AssertValid(Valid());

    [Fact]
    public void Ende_gleich_Beginn_ist_ungueltig()
    {
        var r = Valid();
        r.EndTime = r.StartTime;
        AssertSingleError(r, nameof(SessionRequest.EndTime));
    }

    [Fact]
    public void Ende_vor_Beginn_ist_ungueltig()
    {
        var r = Valid();
        r.StartTime = new TimeOnly(10, 0);
        r.EndTime = new TimeOnly(9, 59);
        AssertSingleError(r, nameof(SessionRequest.EndTime));
    }

    [Fact]
    public void Eine_Minute_Dauer_ist_gueltig()
    {
        var r = Valid();
        r.EndTime = r.StartTime.AddMinutes(1);
        AssertValid(r);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Titel_ist_Pflicht(string? title)
    {
        var r = Valid();
        r.Title = title!;
        AssertSingleError(r, nameof(SessionRequest.Title));
    }

    [Fact]
    public void Titel_hoechstens_300_Zeichen()
    {
        var r = Valid();
        r.Title = new string('x', 301);
        AssertSingleError(r, nameof(SessionRequest.Title));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Raum_ist_Pflicht(int roomId)
    {
        var r = Valid();
        r.RoomId = roomId;
        AssertSingleError(r, nameof(SessionRequest.RoomId));
    }

    [Fact]
    public void Track_und_Speaker_sind_optional()
    {
        var r = Valid();
        r.TrackId = null;
        r.SpeakerIds = [];
        AssertValid(r);
    }
}
