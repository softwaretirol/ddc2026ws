using ConferenceApp.Api.Data;
using ConferenceApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace ConferenceApp.Api.Endpoints;

public static class TrackRoomEndpoints
{
    public static void MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conferences/{conferenceId:int}/tracks").WithTags("Tracks");

        group.MapGet("/", async (int conferenceId, ConferenceDbContext db) =>
            await db.Tracks.AsNoTracking().Where(t => t.ConferenceId == conferenceId).OrderBy(t => t.Name).Select(t => t.ToDto()).ToListAsync());

        group.MapGet("/{id:int}", async Task<IResult> (int conferenceId, int id, ConferenceDbContext db) =>
            await db.Tracks.FirstOrDefaultAsync(t => t.Id == id && t.ConferenceId == conferenceId) is { } t ? TypedResults.Ok(t.ToDto()) : TypedResults.NotFound());

        group.MapPost("/", async Task<IResult> (int conferenceId, TrackRequest req, ConferenceDbContext db) =>
        {
            if (!await db.Conferences.AnyAsync(c => c.Id == conferenceId)) return TypedResults.NotFound();
            var track = new Track { ConferenceId = conferenceId, Name = req.Name.Trim(), Color = req.Color };
            db.Tracks.Add(track);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/conferences/{conferenceId}/tracks/{track.Id}", track.ToDto());
        }).Validate<TrackRequest>();

        group.MapPut("/{id:int}", async Task<IResult> (int conferenceId, int id, TrackRequest req, ConferenceDbContext db) =>
        {
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id && t.ConferenceId == conferenceId);
            if (track is null) return TypedResults.NotFound();
            track.Name = req.Name.Trim();
            track.Color = req.Color;
            await db.SaveChangesAsync();
            return TypedResults.Ok(track.ToDto());
        }).Validate<TrackRequest>();

        // Sessions behalten ihren Platz in der Agenda, der Track wird auf null gesetzt.
        group.MapDelete("/{id:int}", async Task<IResult> (int conferenceId, int id, ConferenceDbContext db) =>
            await db.Tracks.Where(t => t.Id == id && t.ConferenceId == conferenceId).ExecuteDeleteAsync() > 0 ? TypedResults.NoContent() : TypedResults.NotFound());
    }

    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conferences/{conferenceId:int}/rooms").WithTags("Rooms");

        group.MapGet("/", async (int conferenceId, ConferenceDbContext db) =>
            await db.Rooms.AsNoTracking().Where(r => r.ConferenceId == conferenceId).OrderBy(r => r.Id).Select(r => r.ToDto()).ToListAsync());

        group.MapGet("/{id:int}", async Task<IResult> (int conferenceId, int id, ConferenceDbContext db) =>
            await db.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.ConferenceId == conferenceId) is { } r ? TypedResults.Ok(r.ToDto()) : TypedResults.NotFound());

        group.MapPost("/", async Task<IResult> (int conferenceId, RoomRequest req, ConferenceDbContext db) =>
        {
            if (!await db.Conferences.AnyAsync(c => c.Id == conferenceId)) return TypedResults.NotFound();
            var room = new Room { ConferenceId = conferenceId, Name = req.Name.Trim(), Capacity = req.Capacity };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/conferences/{conferenceId}/rooms/{room.Id}", room.ToDto());
        }).Validate<RoomRequest>();

        group.MapPut("/{id:int}", async Task<IResult> (int conferenceId, int id, RoomRequest req, ConferenceDbContext db) =>
        {
            var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.ConferenceId == conferenceId);
            if (room is null) return TypedResults.NotFound();
            room.Name = req.Name.Trim();
            room.Capacity = req.Capacity;
            await db.SaveChangesAsync();
            return TypedResults.Ok(room.ToDto());
        }).Validate<RoomRequest>();

        group.MapDelete("/{id:int}", async Task<IResult> (int conferenceId, int id, ConferenceDbContext db) =>
        {
            // Erst prüfen, ob der Raum zu dieser Konferenz gehört – sonst 404 statt 409 für fremde Räume.
            if (!await db.Rooms.AnyAsync(r => r.Id == id && r.ConferenceId == conferenceId))
                return TypedResults.NotFound();
            if (await db.Sessions.AnyAsync(s => s.RoomId == id))
                return TypedResults.Problem("Der Raum ist noch Sessions zugeordnet.", statusCode: StatusCodes.Status409Conflict);
            return await db.Rooms.Where(r => r.Id == id && r.ConferenceId == conferenceId).ExecuteDeleteAsync() > 0
                ? TypedResults.NoContent() : TypedResults.NotFound();
        });
    }
}
