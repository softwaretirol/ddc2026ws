using ConferenceApp.Shared;
using static ConferenceApp.UnitTests.Validation.ValidationHelper;

namespace ConferenceApp.UnitTests.Validation;

public class TrackRequestTests
{
    [Fact]
    public void Standardwerte_mit_Name_sind_gueltig() => AssertValid(new TrackRequest { Name = ".NET" });

    [Fact]
    public void Name_ist_Pflicht() => AssertSingleError(new TrackRequest { Name = "" }, nameof(TrackRequest.Name));

    [Theory]
    [InlineData("#512BD4")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#aBc123")]
    public void Farbe_gueltig(string color) => AssertValid(new TrackRequest { Name = "T", Color = color });

    [Theory]
    [InlineData("512BD4")]     // ohne #
    [InlineData("#FFF")]       // Kurzform
    [InlineData("#GGGGGG")]    // keine Hex-Ziffern
    [InlineData("#1234567")]   // zu lang
    [InlineData("red")]
    [InlineData("")]
    public void Farbe_ungueltig(string color) =>
        AssertSingleError(new TrackRequest { Name = "T", Color = color }, nameof(TrackRequest.Color));
}

public class RoomRequestTests
{
    [Fact]
    public void Ohne_Kapazitaet_gueltig() => AssertValid(new RoomRequest { Name = "Saal", Capacity = null });

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void Kapazitaet_an_den_Grenzen_gueltig(int capacity) => AssertValid(new RoomRequest { Name = "Saal", Capacity = capacity });

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100_001)]
    public void Kapazitaet_ausserhalb_ungueltig(int capacity) =>
        AssertSingleError(new RoomRequest { Name = "Saal", Capacity = capacity }, nameof(RoomRequest.Capacity));

    [Fact]
    public void Name_ist_Pflicht() => AssertSingleError(new RoomRequest { Name = "" }, nameof(RoomRequest.Name));
}

public class SpeakerRequestTests
{
    [Fact]
    public void Nur_Name_ist_gueltig() => AssertValid(new SpeakerRequest { Name = "Anna" });

    [Fact]
    public void Name_ist_Pflicht() => AssertSingleError(new SpeakerRequest { Name = "" }, nameof(SpeakerRequest.Name));

    [Theory]
    [InlineData("https://example.com/anna.jpg")]
    [InlineData("http://example.com/a.png")]
    public void Foto_URL_gueltig(string url) => AssertValid(new SpeakerRequest { Name = "Anna", PhotoUrl = url });

    [Theory]
    [InlineData(null)]
    [InlineData("")]      // geleertes Formularfeld
    [InlineData("   ")]
    public void Foto_URL_ist_optional(string? url) => AssertValid(new SpeakerRequest { Name = "Anna", PhotoUrl = url });

    [Theory]
    [InlineData("kein-link")]
    [InlineData("example.com/a.png")]
    public void Foto_URL_ungueltig(string url) =>
        AssertSingleError(new SpeakerRequest { Name = "Anna", PhotoUrl = url }, nameof(SpeakerRequest.PhotoUrl));
}
