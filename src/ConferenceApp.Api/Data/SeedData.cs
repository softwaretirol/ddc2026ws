using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.Api.Data;

/// <summary>Spielt Demodaten ein, wenn die Datenbank noch leer ist. Alle Personen sind frei erfunden.</summary>
public static class SeedData
{
    public static async Task SeedAsync(ConferenceDbContext db, CancellationToken ct = default)
    {
        if (await db.Conferences.AnyAsync(ct)) return;

        var day1 = new DateOnly(2026, 12, 1);
        var day2 = day1.AddDays(1);

        var conf = new Conference
        {
            Name = "Dev Conference 2026",
            Slug = "devconf-2026",
            Description = "Zwei Tage rund um .NET, Cloud und Web – mit Talks aus der Praxis.",
            StartDate = day1,
            EndDate = day2,
            Location = "Innsbruck, Congress",
        };

        var dotnet = new Track { Name = ".NET", Color = "#512BD4" };
        var web = new Track { Name = "Web & Frontend", Color = "#E65100" };
        var cloud = new Track { Name = "Cloud & DevOps", Color = "#00897B" };
        conf.Tracks.AddRange([dotnet, web, cloud]);

        var saal = new Room { Name = "Saal Tirol", Capacity = 400 };
        var raumA = new Room { Name = "Raum A", Capacity = 120 };
        var raumB = new Room { Name = "Raum B", Capacity = 80 };
        conf.Rooms.AddRange([saal, raumA, raumB]);

        var anna = new Speaker { Name = "Anna Berger", Company = "Contoso", Bio = "Architektin mit Fokus auf verteilte Systeme.", Links = ["https://example.com/anna"] };
        var ben = new Speaker { Name = "Ben Huber", Company = "Fabrikam", Bio = "Frontend-Entwickler und Blazor-Fan." };
        var clara = new Speaker { Name = "Clara Wolf", Company = "Northwind", Bio = "Cloud-Engineer, liebt Automatisierung." };
        var david = new Speaker { Name = "David Novak", Company = "Adventure Works", Bio = "Performance-Tuning und Diagnose in .NET." };
        var eva = new Speaker { Name = "Eva Steiner", Company = "Tailspin", Bio = "UX und Barrierefreiheit im Web." };
        db.Speakers.AddRange(anna, ben, clara, david, eva);

        Session S(string title, DateOnly day, int h, int m, int minutes, Room room, Track? track, params Speaker[] speakers)
        {
            var start = new TimeOnly(h, m);
            return new Session
            {
                Title = title,
                Abstract = $"In diesem Talk geht es um {title}.",
                Day = day, StartTime = start, EndTime = start.AddMinutes(minutes),
                Room = room, Track = track, Speakers = [.. speakers],
            };
        }

        conf.Sessions.AddRange(
        [
            S("Keynote: Software für die nächsten 10 Jahre", day1, 9, 0, 60, saal, null, anna),
            S("Was ist neu in .NET 11", day1, 10, 30, 45, saal, dotnet, david),
            S("Blazor WebAssembly in der Praxis", day1, 10, 30, 45, raumA, web, ben),
            S("Postgres für .NET-Entwickler", day1, 10, 30, 45, raumB, cloud, clara),
            S("Minimal APIs richtig strukturieren", day1, 13, 0, 45, saal, dotnet, anna, david),
            S("Barrierefreie Web-UIs", day1, 13, 0, 45, raumA, web, eva),
            S("Aspire: Lokale Orchestrierung", day2, 9, 0, 45, saal, cloud, clara),
            S("Performance-Profiling mit dotnet-trace", day2, 9, 0, 45, raumA, dotnet, david),
            S("Design-Systeme mit MudBlazor", day2, 10, 0, 45, raumB, web, ben, eva),
            S("Closing Panel", day2, 15, 0, 60, saal, null, anna, ben, clara),
        ]);

        db.Conferences.Add(conf);
        await db.SaveChangesAsync(ct);
    }
}
