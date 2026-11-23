using ConferenceApp.Api.Data;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.Api.Endpoints;

public static class ConferenceEndpoints
{
    public static void MapConferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conferences").WithTags("Conferences");

        // Sessionanzahl als korrelierte Subquery in derselben Abfrage (kein N+1).
        group.MapGet("/", async (ConferenceDbContext db) =>
            await db.Conferences.AsNoTracking()
                .OrderByDescending(c => c.StartDate)
                .Select(c => new ConferenceDto(c.Id, c.Name, c.Slug, c.Description, c.StartDate, c.EndDate, c.Location, c.Sessions.Count()))
                .ToListAsync());

        group.MapGet("/{id:int}", async Task<IResult> (int id, ConferenceDbContext db) =>
            await db.Conferences.FindAsync(id) is { } c ? TypedResults.Ok(c.ToDto()) : TypedResults.NotFound());

        group.MapGet("/{slug}/agenda", async Task<IResult> (string slug, ConferenceDbContext db) =>
        {
            var conf = await db.Conferences.AsNoTracking()
                .Include(c => c.Tracks)
                .Include(c => c.Rooms)
                .Include(c => c.Sessions).ThenInclude(s => s.Room)
                .Include(c => c.Sessions).ThenInclude(s => s.Track)
                .Include(c => c.Sessions).ThenInclude(s => s.Speakers)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Slug == slug);
            if (conf is null) return TypedResults.NotFound();

            return TypedResults.Ok(new AgendaDto(
                conf.ToDto(),
                conf.Tracks.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList(),
                conf.Rooms.OrderBy(r => r.Id).Select(r => r.ToDto()).ToList(),
                conf.Sessions.OrderBy(s => s.Day).ThenBy(s => s.StartTime).ThenBy(s => s.RoomId).Select(s => s.ToDto()).ToList()));
        });

        // Abonnierbarer Kalender (RFC 5545) mit allen Sessions, optional auf einen Track gefiltert.
        group.MapGet("/{slug}/agenda.ics", async Task<IResult> (string slug, int? track, ConferenceDbContext db, HttpContext http) =>
        {
            var conf = await db.Conferences.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug);
            if (conf is null) return TypedResults.NotFound();

            var query = db.Sessions.AsNoTracking()
                .Include(s => s.Room)
                .Include(s => s.Track)
                .Include(s => s.Speakers)
                .AsSplitQuery()
                .Where(s => s.ConferenceId == conf.Id);
            string? trackName = null;
            if (track is { } trackId)
            {
                trackName = await db.Tracks.Where(t => t.Id == trackId && t.ConferenceId == conf.Id).Select(t => t.Name).FirstOrDefaultAsync();
                if (trackName is null) return TypedResults.NotFound();
                query = query.Where(s => s.TrackId == trackId);
            }

            var sessions = (await query.OrderBy(s => s.Day).ThenBy(s => s.StartTime).ThenBy(s => s.RoomId).ToListAsync())
                .Select(s => s.ToDto());
            var calendarName = trackName is null ? conf.Name : $"{conf.Name} – {trackName}";
            var ics = IcsBuilder.Build(calendarName, conf.ToDto(), sessions);

            var fileName = IcsBuilder.FileName(trackName is null ? conf.Slug : $"{conf.Slug}-{trackName}");
            http.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
            return TypedResults.Text(ics, IcsBuilder.ContentType + "; charset=utf-8");
        });

        group.MapPost("/", async Task<IResult> (ConferenceRequest req, ConferenceDbContext db) =>
        {
            if (await db.Conferences.AnyAsync(c => c.Slug == req.Slug))
                return ValidationExtensions.Problem(nameof(req.Slug), "Dieser Slug ist bereits vergeben.");

            var conf = new Conference { Name = req.Name, Slug = req.Slug };
            Apply(conf, req);
            db.Conferences.Add(conf);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/conferences/{conf.Id}", conf.ToDto());
        }).Validate<ConferenceRequest>();

        group.MapPut("/{id:int}", async Task<IResult> (int id, ConferenceRequest req, ConferenceDbContext db) =>
        {
            var conf = await db.Conferences.FindAsync(id);
            if (conf is null) return TypedResults.NotFound();
            if (await db.Conferences.AnyAsync(c => c.Slug == req.Slug && c.Id != id))
                return ValidationExtensions.Problem(nameof(req.Slug), "Dieser Slug ist bereits vergeben.");
            if (await db.Sessions.AnyAsync(s => s.ConferenceId == id && (s.Day < req.StartDate || s.Day > req.EndDate)))
                return ValidationExtensions.Problem(nameof(req.EndDate), "Es gibt Sessions außerhalb des neuen Zeitraums.");

            Apply(conf, req);
            await db.SaveChangesAsync();
            return TypedResults.Ok(conf.ToDto());
        }).Validate<ConferenceRequest>();

        group.MapDelete("/{id:int}", async Task<IResult> (int id, ConferenceDbContext db) =>
            await db.Conferences.Where(c => c.Id == id).ExecuteDeleteAsync() > 0 ? TypedResults.NoContent() : TypedResults.NotFound());
    }

    private static void Apply(Conference c, ConferenceRequest r)
    {
        c.Name = r.Name.Trim();
        c.Slug = r.Slug.Trim();
        c.Description = r.Description;
        c.StartDate = r.StartDate;
        c.EndDate = r.EndDate;
        c.Location = r.Location;
    }
}
