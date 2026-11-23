using ConferenceApp.Api.Endpoints;
using ConferenceApp.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ConferenceApp.UnitTests.Api;

public class ValidationFilterTests
{
    private static readonly object NextResult = new();

    private static async Task<(object? Result, bool NextCalled)> RunAsync<T>(params object?[] args) where T : class
    {
        var nextCalled = false;
        var ctx = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), args);
        var result = await new ValidationFilter<T>().InvokeAsync(ctx, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(NextResult);
        });
        return (result, nextCalled);
    }

    [Fact]
    public async Task Gueltiges_Modell_ruft_den_Endpunkt_auf()
    {
        var model = new RoomRequest { Name = "Saal", Capacity = 10 };
        var (result, nextCalled) = await RunAsync<RoomRequest>(1, model);

        Assert.True(nextCalled);
        Assert.Same(NextResult, result);
    }

    [Fact]
    public async Task Ungueltiges_Modell_liefert_ValidationProblem_mit_Feldfehlern()
    {
        var model = new ConferenceRequest { Name = "", Slug = "Ungültig" };
        var (result, nextCalled) = await RunAsync<ConferenceRequest>(model);

        Assert.False(nextCalled);
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(["Name ist erforderlich."], problem.ProblemDetails.Errors["Name"]);
        Assert.Equal(["Nur Kleinbuchstaben, Ziffern und Bindestriche."], problem.ProblemDetails.Errors["Slug"]);
    }

    [Fact]
    public async Task Fehler_aus_IValidatableObject_landen_beim_Feld()
    {
        var model = new SessionRequest { Title = "T", RoomId = 1, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 0) };
        var (result, nextCalled) = await RunAsync<SessionRequest>(1, model);

        Assert.False(nextCalled);
        var problem = Assert.IsType<ValidationProblem>(result);
        var error = Assert.Single(problem.ProblemDetails.Errors);
        Assert.Equal("EndTime", error.Key);
        Assert.Equal(["Die Session muss nach ihrem Beginn enden."], error.Value);
    }

    [Fact]
    public async Task Fehlender_Body_liefert_400()
    {
        var (result, nextCalled) = await RunAsync<RoomRequest>(1);

        Assert.False(nextCalled);
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void Problem_Helfer_liefert_ValidationProblem_fuer_ein_Feld()
    {
        var result = Assert.IsType<ValidationProblem>(ValidationExtensions.Problem("Slug", "Dieser Slug ist bereits vergeben."));
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(["Dieser Slug ist bereits vergeben."], result.ProblemDetails.Errors["Slug"]);
    }
}
