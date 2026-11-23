using System.ComponentModel.DataAnnotations;

namespace ConferenceApp.Shared;

// ---------- Lese-DTOs ----------

public record ConferenceDto(int Id, string Name, string Slug, string? Description, DateOnly StartDate, DateOnly EndDate, string? Location);

public record TrackDto(int Id, int ConferenceId, string Name, string Color);

public record RoomDto(int Id, int ConferenceId, string Name, int? Capacity);

public record SpeakerDto(int Id, string Name, string? Bio, string? Company, string? PhotoUrl, List<string> Links);

public record SpeakerSummaryDto(int Id, string Name, string? Company, string? PhotoUrl);

public record SessionDto(
    int Id,
    int ConferenceId,
    string Title,
    string? Abstract,
    DateOnly Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RoomId,
    string RoomName,
    int? TrackId,
    string? TrackName,
    string? TrackColor,
    List<SpeakerSummaryDto> Speakers);

public record AgendaDto(ConferenceDto Conference, List<TrackDto> Tracks, List<RoomDto> Rooms, List<SessionDto> Sessions);

public record SpeakerSessionDto(int Id, string Title, string ConferenceName, string ConferenceSlug, DateOnly Day, TimeOnly StartTime, TimeOnly EndTime, string RoomName);

public record SpeakerDetailDto(SpeakerDto Speaker, List<SpeakerSessionDto> Sessions);

// ---------- Schreib-Requests (mutable, damit Formulare direkt binden koennen) ----------

public class ConferenceRequest : IValidatableObject
{
    [Required(ErrorMessage = "Name ist erforderlich."), MaxLength(200)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Slug ist erforderlich."), MaxLength(100)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Nur Kleinbuchstaben, Ziffern und Bindestriche.")]
    public string Slug { get; set; } = "";

    [MaxLength(4000)]
    public string? Description { get; set; }

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(200)]
    public string? Location { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
            yield return new ValidationResult("Das Enddatum darf nicht vor dem Startdatum liegen.", [nameof(EndDate)]);
    }
}

public class TrackRequest
{
    [Required(ErrorMessage = "Name ist erforderlich."), MaxLength(100)]
    public string Name { get; set; } = "";

    [Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Farbe im Format #RRGGBB.")]
    public string Color { get; set; } = "#1E88E5";
}

public class RoomRequest
{
    [Required(ErrorMessage = "Name ist erforderlich."), MaxLength(100)]
    public string Name { get; set; } = "";

    [Range(1, 100_000, ErrorMessage = "Kapazität muss größer als 0 sein.")]
    public int? Capacity { get; set; }
}

public class SpeakerRequest
{
    [Required(ErrorMessage = "Name ist erforderlich."), MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(4000)]
    public string? Bio { get; set; }

    [MaxLength(200)]
    public string? Company { get; set; }

    // Optional: ein geleertes Formularfeld ("" bzw. nur Leerzeichen) gilt als "kein Foto", nicht als ungültige URL.
    [Url(ErrorMessage = "Keine gültige URL."), MaxLength(500)]
    public string? PhotoUrl { get => _photoUrl; set => _photoUrl = string.IsNullOrWhiteSpace(value) ? null : value; }
    private string? _photoUrl;

    public List<string> Links { get; set; } = [];
}

public class SessionRequest : IValidatableObject
{
    [Required(ErrorMessage = "Titel ist erforderlich."), MaxLength(300)]
    public string Title { get; set; } = "";

    [MaxLength(8000)]
    public string? Abstract { get; set; }

    public DateOnly Day { get; set; }
    public TimeOnly StartTime { get; set; } = new(9, 0);
    public TimeOnly EndTime { get; set; } = new(10, 0);

    [Range(1, int.MaxValue, ErrorMessage = "Raum ist erforderlich.")]
    public int RoomId { get; set; }

    public int? TrackId { get; set; }

    public List<int> SpeakerIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndTime <= StartTime)
            yield return new ValidationResult("Die Session muss nach ihrem Beginn enden.", [nameof(EndTime)]);
    }
}
