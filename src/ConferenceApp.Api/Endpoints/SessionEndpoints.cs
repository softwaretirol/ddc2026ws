using ConferenceApp.Api.Data;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conferences/{conferenceId:int}/sessions").WithTags("Sessions");

        group.MapGet("/", async (int conferenceId, ConferenceDbContext db) =>
            (await Query(db).Where(s => s.ConferenceId == conferenceId)
                .OrderBy(s => s.Day).ThenBy(s => s.StartTime).ThenBy(s => s.RoomId).ToListAsync())
            .Select(s => s.ToDto()).ToList());

        group.MapGet("/{id:int}", async Task<IResult> (int conferenceId, int id, ConferenceDbContext db) =>
            await Query(db).FirstOrDefaultAsync(s => s.Id == id && s.ConferenceId == conferenceId) is { } s
                ? TypedResults.Ok(s.ToDto()) : TypedResults.NotFound());

        group.MapPost("/", async Task<IResult> (int conferenceId, SessionRequest req, ConferenceDbContext db) =>
        {
            var conf = await db.Conferences.FindAsync(conferenceId);
            if (conf is null) return TypedResults.NotFound();

            var session = new Session { ConferenceId = conferenceId, Title = req.Title };
            if (await ApplyAsync(session, req, conf, db) is { } problem) return problem;

            db.Sessions.Add(session);
            await db.SaveChangesAsync();
            var created = await Query(db).FirstAsync(s => s.Id == session.Id);
            return TypedResults.Created($"/api/conferences/{conferenceId}/sessions/{session.Id}", created.ToDto());
        }).Validate<SessionRequest>();

        group.MapPut("/{id:int}", async Task<IResult> (int conferenceId, int id, SessionRequest req, ConferenceDbContext db) =>
        {
            var conf = await db.Conferences.FindAsync(conferenceId);
            var session = await db.Sessions.Include(s => s.Speakers).FirstOrDefaultAsync(s => s.Id == id && s.ConferenceId == conferenceId);
            if (conf is null || session is null) return TypedResults.NotFound();

            if (await ApplyAsync(session, req, conf, db) is { } problem) return problem;

            await db.SaveChangesAsync();
            var updated = await Query(db).FirstAsync(s => s.Id == id);
            return TypedResults.Ok(updated.ToDto());
        }).Validate<SessionRequest>();

        group.MapDelete("/{id:int}", async Task<IResult> (int conferenceId, int id, ConferenceDbContext db) =>
            await db.Sessions.Where(s => s.Id == id && s.ConferenceId == conferenceId).ExecuteDeleteAsync() > 0
                ? TypedResults.NoContent() : TypedResults.NotFound());
    }

    private static IQueryable<Session> Query(ConferenceDbContext db) =>
        db.Sessions.AsNoTracking().Include(s => s.Room).Include(s => s.Track).Include(s => s.Speakers);

    /// <summary>Prueft die Fachregeln und uebernimmt die Werte. Liefert ein Problem-Ergebnis oder null.</summary>
    private static async Task<IResult?> ApplyAsync(Session session, SessionRequest req, Conference conf, ConferenceDbContext db)
    {
        if (req.Day < conf.StartDate || req.Day > conf.EndDate)
            return ValidationExtensions.Problem(nameof(req.Day), $"Der Tag muss zwischen {conf.StartDate:dd.MM.yyyy} und {conf.EndDate:dd.MM.yyyy} liegen.");

        if (!await db.Rooms.AnyAsync(r => r.Id == req.RoomId && r.ConferenceId == conf.Id))
            return ValidationExtensions.Problem(nameof(req.RoomId), "Der Raum gehört nicht zu dieser Konferenz.");

        if (req.TrackId is { } trackId && !await db.Tracks.AnyAsync(t => t.Id == trackId && t.ConferenceId == conf.Id))
            return ValidationExtensions.Problem(nameof(req.TrackId), "Der Track gehört nicht zu dieser Konferenz.");

        var clash = await db.Sessions.AsNoTracking()
            .Where(s => s.Id != session.Id && s.RoomId == req.RoomId && s.Day == req.Day
                        && s.StartTime < req.EndTime && req.StartTime < s.EndTime)
            .Select(s => new { s.Title, s.StartTime, s.EndTime })
            .FirstOrDefaultAsync();
        if (clash is not null)
            return ValidationExtensions.Problem(nameof(req.StartTime),
                $"Überschneidung im Raum mit „{clash.Title}“ ({clash.StartTime:HH\\:mm}–{clash.EndTime:HH\\:mm}).");

        var speakerIds = req.SpeakerIds.Distinct().ToList();
        var speakers = await db.Speakers.Where(s => speakerIds.Contains(s.Id)).ToListAsync();
        if (speakers.Count != speakerIds.Count)
            return ValidationExtensions.Problem(nameof(req.SpeakerIds), "Mindestens ein Speaker existiert nicht.");

        session.Title = req.Title.Trim();
        session.Abstract = req.Abstract;
        session.Day = req.Day;
        session.StartTime = req.StartTime;
        session.EndTime = req.EndTime;
        session.RoomId = req.RoomId;
        session.TrackId = req.TrackId;
        session.Speakers.Clear();
        session.Speakers.AddRange(speakers);
        return null;
    }
}
