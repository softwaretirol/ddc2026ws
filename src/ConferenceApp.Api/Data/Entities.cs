namespace ConferenceApp.Api.Data;

public class Conference
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Location { get; set; }

    public List<Track> Tracks { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
    public List<Session> Sessions { get; set; } = [];
}

public class Track
{
    public int Id { get; set; }
    public int ConferenceId { get; set; }
    public Conference Conference { get; set; } = null!;
    public required string Name { get; set; }
    public required string Color { get; set; }
}

public class Room
{
    public int Id { get; set; }
    public int ConferenceId { get; set; }
    public Conference Conference { get; set; } = null!;
    public required string Name { get; set; }
    public int? Capacity { get; set; }
}

public class Speaker
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Bio { get; set; }
    public string? Company { get; set; }
    public string? PhotoUrl { get; set; }
    public List<string> Links { get; set; } = [];

    public List<Session> Sessions { get; set; } = [];
}

public class Session
{
    public int Id { get; set; }
    public int ConferenceId { get; set; }
    public Conference Conference { get; set; } = null!;
    public required string Title { get; set; }
    public string? Abstract { get; set; }

    /// <summary>Konferenztag; alle Zeiten sind lokale Zeit der Konferenz.</summary>
    public DateOnly Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int? TrackId { get; set; }
    public Track? Track { get; set; }

    /// <summary>n:m ueber die Join-Tabelle SessionSpeaker.</summary>
    public List<Speaker> Speakers { get; set; } = [];
}
