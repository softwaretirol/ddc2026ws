using ConferenceApp.Client.Components;

namespace ConferenceApp.UnitTests.Client;

/// <summary>#8: Kontrast nach WCAG 2.x; AA verlangt 4,5:1 für Text.</summary>
public class ColorContrastTests
{
    [Theory]
    [InlineData("#000000", "#FFFFFF", 21.0)]
    [InlineData("#FFFFFF", "#FFFFFF", 1.0)]
    [InlineData("#BF360C", "#FFFFFF", 5.60)]   // neue Seed-Farbe Web & Frontend
    [InlineData("#00796B", "#FFFFFF", 5.32)]   // neue Seed-Farbe Cloud & DevOps
    [InlineData("#E65100", "#FFFFFF", 3.79)]   // alte Seed-Farbe, laut Issue ca. 3,8:1
    [InlineData("#00897B", "#FFFFFF", 4.32)]   // alte Seed-Farbe, laut Issue ca. 4,3:1
    [InlineData("#767676", "#FFFFFF", 4.54)]   // bekannte Grenzfarbe für AA
    public void Kontrastverhaeltnis_nach_WCAG(string a, string b, double expected) =>
        Assert.Equal(expected, ColorContrast.Ratio(a, b)!.Value, 0.02);

    [Fact]
    public void Kontrast_ist_symmetrisch() =>
        Assert.Equal(ColorContrast.Ratio("#BF360C", "#FFFFFF"), ColorContrast.Ratio("#FFFFFF", "#BF360C"));

    [Fact]
    public void Alte_Seed_Farben_unter_AA_neue_darueber()
    {
        Assert.True(ColorContrast.Ratio("#E65100", ColorContrast.White) < ColorContrast.MinText);
        Assert.True(ColorContrast.Ratio("#00897B", ColorContrast.White) < ColorContrast.MinText);
        Assert.True(ColorContrast.Ratio("#BF360C", ColorContrast.White) >= ColorContrast.MinText);
        Assert.True(ColorContrast.Ratio("#00796B", ColorContrast.White) >= ColorContrast.MinText);
    }

    [Theory]
    [InlineData("#512BD4", ColorContrast.White)]
    [InlineData("#000000", ColorContrast.White)]
    [InlineData("#FFEB3B", ColorContrast.Black)]
    [InlineData("#FFFFFF", ColorContrast.Black)]
    public void Textfarbe_auf_Track_Flaeche(string background, string expected) =>
        Assert.Equal(expected, ColorContrast.TextOn(background));

    [Fact]
    public void Textfarbe_hat_immer_mindestens_AA_Kontrast_auf_Seed_Farben()
    {
        foreach (var c in new[] { "#512BD4", "#BF360C", "#00796B" })
            Assert.True(ColorContrast.Ratio(c, ColorContrast.TextOn(c)) >= ColorContrast.MinText, c);
    }

    [Theory]
    [InlineData("blau")]
    [InlineData("#12345")]
    [InlineData("")]
    [InlineData(null)]
    public void Ungueltige_Farbe_liefert_null(string? hex) => Assert.Null(ColorContrast.Ratio(hex, "#FFFFFF"));

    [Fact]
    public void Kurzform_wird_verstanden() => Assert.Equal(21.0, ColorContrast.Ratio("#000", "#fff")!.Value, 0.01);

    [Fact]
    public void Formatierung_deutsch() => Assert.Equal("5,6:1", ColorContrast.FormatRatio(5.6));
}
