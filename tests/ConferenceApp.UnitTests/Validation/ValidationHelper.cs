using System.ComponentModel.DataAnnotations;

namespace ConferenceApp.UnitTests.Validation;

/// <summary>Validiert wie die API (DataAnnotations + IValidatableObject, alle Properties).</summary>
internal static class ValidationHelper
{
    public static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    public static void AssertValid(object model)
    {
        var results = Validate(model);
        Assert.True(results.Count == 0,
            "Erwartet gültig, aber: " + string.Join("; ", results.Select(r => $"[{string.Join(",", r.MemberNames)}] {r.ErrorMessage}")));
    }

    /// <summary>Prüft, dass genau das angegebene Feld (und nur dieses) einen Fehler hat.</summary>
    public static ValidationResult AssertSingleError(object model, string member)
    {
        var results = Validate(model);
        var result = Assert.Single(results);
        Assert.Contains(member, result.MemberNames);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        return result;
    }
}
