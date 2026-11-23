using ConferenceApp.Api.Data;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.Api.Endpoints;

public static class SpeakerEndpoints
{
    public static void MapSpeakerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/speakers").WithTags("Speakers");

        group.MapGet("/", async (ConferenceDbContext db) =>
            (await db.Speakers.AsNoTracking().OrderBy(s => s.Name).ToListAsync()).Select(s => s.ToDto()).ToList());

        group.MapGet("/{id:int}", async Task<IResult> (int id, ConferenceDbContext db) =>
        {
            var speaker = await db.Speakers.AsNoTracking()
                .Include(s => s.Sessions).ThenInclude(x => x.Conference)
                .Include(s => s.Sessions).ThenInclude(x => x.Room)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == id);
            if (speaker is null) return TypedResults.NotFound();

            var sessions = speaker.Sessions
                .OrderBy(x => x.Day).ThenBy(x => x.StartTime)
                .Select(x => new SpeakerSessionDto(x.Id, x.Title, x.Conference.Name, x.Conference.Slug, x.Day, x.StartTime, x.EndTime, x.Room.Name))
                .ToList();
            return TypedResults.Ok(new SpeakerDetailDto(speaker.ToDto(), sessions));
        });

        group.MapPost("/", async Task<IResult> (SpeakerRequest req, ConferenceDbContext db) =>
        {
            var speaker = new Speaker { Name = req.Name };
            Apply(speaker, req);
            db.Speakers.Add(speaker);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/speakers/{speaker.Id}", speaker.ToDto());
        }).Validate<SpeakerRequest>();

        group.MapPut("/{id:int}", async Task<IResult> (int id, SpeakerRequest req, ConferenceDbContext db) =>
        {
            var speaker = await db.Speakers.FindAsync(id);
            if (speaker is null) return TypedResults.NotFound();
            Apply(speaker, req);
            await db.SaveChangesAsync();
            return TypedResults.Ok(speaker.ToDto());
        }).Validate<SpeakerRequest>();

        // Zuordnungen in SessionSpeaker werden per Cascade entfernt.
        group.MapDelete("/{id:int}", async Task<IResult> (int id, ConferenceDbContext db) =>
            await db.Speakers.Where(s => s.Id == id).ExecuteDeleteAsync() > 0 ? TypedResults.NoContent() : TypedResults.NotFound());
    }

    private static void Apply(Speaker s, SpeakerRequest r)
    {
        s.Name = r.Name.Trim();
        s.Bio = r.Bio;
        s.Company = r.Company;
        s.PhotoUrl = string.IsNullOrWhiteSpace(r.PhotoUrl) ? null : r.PhotoUrl.Trim();
        s.Links = r.Links.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
    }
}
