using ConferenceApp.Shared;

namespace ConferenceApp.UnitTests.Client;

internal static class TestData
{
    public static readonly DateOnly Day1 = new(2026, 12, 1);
    public static readonly DateOnly Day2 = new(2026, 12, 2);

    public static ConferenceDto Conference(DateOnly? start = null, DateOnly? end = null, string? location = "Innsbruck") =>
        new(1, "Dev Conference 2026", "devconf-2026", null, start ?? Day1, end ?? Day2, location);

    public static SessionDto Session(int id, string start, string end, int roomId = 1, DateOnly? day = null,
        string? title = null, string roomName = "Saal", string? track = null, string? @abstract = null, params SpeakerSummaryDto[] speakers) =>
        new(id, 1, title ?? $"Session {id}", @abstract, day ?? Day1, TimeOnly.Parse(start), TimeOnly.Parse(end),
            roomId, roomName, track is null ? null : 7, track, track is null ? null : "#512BD4", [.. speakers]);

    public static SpeakerSummaryDto Speaker(int id, string name) => new(id, name, null, null);
}
