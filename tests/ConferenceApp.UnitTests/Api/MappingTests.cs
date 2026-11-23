using ConferenceApp.Api.Data;
using ConferenceApp.Api.Endpoints;

namespace ConferenceApp.UnitTests.Api;

public class MappingTests
{
    private static readonly Conference Conf = new() { Id = 3, Name = "DDC", Slug = "ddc", StartDate = new DateOnly(2026, 12, 1), EndDate = new DateOnly(2026, 12, 2), Location = "Innsbruck", Description = "D" };

    [Fact]
    public void Konferenz_wird_vollstaendig_abgebildet()
    {
        var dto = Conf.ToDto();
        Assert.Equal((3, "DDC", "ddc", "D", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2), "Innsbruck"),
            (dto.Id, dto.Name, dto.Slug, dto.Description, dto.StartDate, dto.EndDate, dto.Location));
    }

    [Fact]
    public void Session_mit_Track_und_nach_Name_sortierten_Speakern()
    {
        var room = new Room { Id = 7, ConferenceId = 3, Name = "Saal" };
        var track = new Track { Id = 9, ConferenceId = 3, Name = ".NET", Color = "#512BD4" };
        var session = new Session
        {
            Id = 11, ConferenceId = 3, Title = "Talk", Abstract = "A",
            Day = new DateOnly(2026, 12, 1), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 45),
            RoomId = 7, Room = room, TrackId = 9, Track = track,
            Speakers = [new Speaker { Id = 2, Name = "Zoe" }, new Speaker { Id = 1, Name = "Anna", Company = "Contoso" }],
        };

        var dto = session.ToDto();

        Assert.Equal("Saal", dto.RoomName);
        Assert.Equal(9, dto.TrackId);
        Assert.Equal(".NET", dto.TrackName);
        Assert.Equal("#512BD4", dto.TrackColor);
        Assert.Equal(["Anna", "Zoe"], dto.Speakers.Select(s => s.Name));
        Assert.Equal("Contoso", dto.Speakers[0].Company);
    }

    [Fact]
    public void Session_ohne_Track_hat_leere_Trackfelder()
    {
        var session = new Session
        {
            Id = 1, ConferenceId = 3, Title = "Keynote",
            Day = new DateOnly(2026, 12, 1), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0),
            RoomId = 7, Room = new Room { Id = 7, Name = "Saal" },
        };

        var dto = session.ToDto();

        Assert.Null(dto.TrackId);
        Assert.Null(dto.TrackName);
        Assert.Null(dto.TrackColor);
        Assert.Empty(dto.Speakers);
    }

    [Fact]
    public void Speaker_mit_Links()
    {
        var dto = new Speaker { Id = 4, Name = "Anna", Links = ["https://a.example"] }.ToDto();
        Assert.Equal(["https://a.example"], dto.Links);
        Assert.Equal(4, dto.Id);
    }
}
