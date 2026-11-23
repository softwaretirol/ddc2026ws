using System.ComponentModel.DataAnnotations;
using ConferenceApp.Api.Data;
using ConferenceApp.Shared;

namespace ConferenceApp.Api.Endpoints;

/// <summary>Validiert den Request-Body per DataAnnotations/IValidatableObject und liefert 400 als ValidationProblem.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var model = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
            return TypedResults.Problem("Request-Body fehlt.", statusCode: StatusCodes.Status400BadRequest);

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""]).Select(m => (Member: m, r.ErrorMessage)))
                .GroupBy(x => x.Member)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Ungültig").ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        return await next(ctx);
    }
}

public static class ValidationExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();

    public static IResult Problem(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}

public static class Mapping
{
    public static ConferenceDto ToDto(this Conference c) => new(c.Id, c.Name, c.Slug, c.Description, c.StartDate, c.EndDate, c.Location);
    public static TrackDto ToDto(this Track t) => new(t.Id, t.ConferenceId, t.Name, t.Color);
    public static RoomDto ToDto(this Room r) => new(r.Id, r.ConferenceId, r.Name, r.Capacity);
    public static SpeakerDto ToDto(this Speaker s) => new(s.Id, s.Name, s.Bio, s.Company, s.PhotoUrl, s.Links);
    public static SpeakerSummaryDto ToSummary(this Speaker s) => new(s.Id, s.Name, s.Company, s.PhotoUrl);

    /// <summary>Erwartet geladene Room-, Track- und Speakers-Navigationen.</summary>
    public static SessionDto ToDto(this Session s) => new(
        s.Id, s.ConferenceId, s.Title, s.Abstract, s.Day, s.StartTime, s.EndTime,
        s.RoomId, s.Room.Name, s.TrackId, s.Track?.Name, s.Track?.Color,
        s.Speakers.OrderBy(x => x.Name).Select(x => x.ToSummary()).ToList());
}
