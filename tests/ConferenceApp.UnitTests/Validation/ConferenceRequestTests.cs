using ConferenceApp.Shared;
using static ConferenceApp.UnitTests.Validation.ValidationHelper;

namespace ConferenceApp.UnitTests.Validation;

public class ConferenceRequestTests
{
    private static ConferenceRequest Valid() => new()
    {
        Name = "DDC 2027",
        Slug = "ddc-2027",
        Description = "Beschreibung",
        StartDate = new DateOnly(2027, 11, 22),
        EndDate = new DateOnly(2027, 11, 24),
        Location = "Innsbruck",
    };

    [Fact]
    public void Gueltige_Konferenz_hat_keine_Fehler() => AssertValid(Valid());

    [Fact]
    public void Optionale_Felder_duerfen_fehlen()
    {
        var r = Valid();
        r.Description = null;
        r.Location = null;
        AssertValid(r);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Name_ist_Pflicht(string? name)
    {
        var r = Valid();
        r.Name = name!;
        AssertSingleError(r, nameof(ConferenceRequest.Name));
    }

    [Fact]
    public void Name_hoechstens_200_Zeichen()
    {
        var r = Valid();
        r.Name = new string('x', 200);
        AssertValid(r);
        r.Name = new string('x', 201);
        AssertSingleError(r, nameof(ConferenceRequest.Name));
    }

    [Fact]
    public void Slug_ist_Pflicht()
    {
        var r = Valid();
        r.Slug = "";
        AssertSingleError(r, nameof(ConferenceRequest.Slug));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ddc")]
    [InlineData("ddc-2027")]
    [InlineData("2027")]
    [InlineData("dev-conf-2027-wien")]
    public void Slug_gueltige_Formate(string slug)
    {
        var r = Valid();
        r.Slug = slug;
        AssertValid(r);
    }

    [Theory]
    [InlineData("DDC-2027")]      // Großbuchstaben
    [InlineData("ddc 2027")]      // Leerzeichen
    [InlineData("ddc_2027")]      // Unterstrich
    [InlineData("-ddc")]          // führender Bindestrich
    [InlineData("ddc-")]          // abschließender Bindestrich
    [InlineData("ddc--2027")]     // doppelter Bindestrich
    [InlineData("köln")]          // Umlaut
    [InlineData("ddc/2027")]      // Pfadtrenner
    [InlineData(" ddc")]          // führendes Leerzeichen
    public void Slug_ungueltige_Formate(string slug)
    {
        var r = Valid();
        r.Slug = slug;
        AssertSingleError(r, nameof(ConferenceRequest.Slug));
    }

    [Fact]
    public void Slug_hoechstens_100_Zeichen()
    {
        var r = Valid();
        r.Slug = new string('a', 100);
        AssertValid(r);
        r.Slug = new string('a', 101);
        AssertSingleError(r, nameof(ConferenceRequest.Slug));
    }

    [Fact]
    public void Enddatum_vor_Startdatum_ist_ungueltig()
    {
        var r = Valid();
        r.StartDate = new DateOnly(2027, 11, 24);
        r.EndDate = new DateOnly(2027, 11, 23);
        AssertSingleError(r, nameof(ConferenceRequest.EndDate));
    }

    [Fact]
    public void Eintaegige_Konferenz_ist_gueltig()
    {
        var r = Valid();
        r.EndDate = r.StartDate;
        AssertValid(r);
    }

    [Fact]
    public void Beschreibung_und_Ort_haben_Laengengrenzen()
    {
        var r = Valid();
        r.Description = new string('x', 4001);
        AssertSingleError(r, nameof(ConferenceRequest.Description));

        r = Valid();
        r.Location = new string('x', 201);
        AssertSingleError(r, nameof(ConferenceRequest.Location));
    }
}
