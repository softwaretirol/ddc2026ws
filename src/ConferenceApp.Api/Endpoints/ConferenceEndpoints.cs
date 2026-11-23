using ConferenceApp.Api.Data;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.Api.Endpoints;

public static class ConferenceEndpoints
{
    public static void MapConferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conferences").WithTags("Conferences");

        group.MapGet("/", async (ConferenceDbContext db) =>
            await db.Conferences.AsNoTracking().OrderByDescending(c => c.StartDate).Select(c => c.ToDto()).ToListAsync());

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
