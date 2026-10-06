using Factory.Adapters.Storage;

namespace Factory.Adapters.Tests.Storage;

public class ResultCheckTests
{
    [Theory]
    [InlineData("plan_ready")]
    [InlineData("done")]
    [InlineData("needs_input")]
    [InlineData("blocked")]
    public void A_result_with_a_known_status_has_no_problem(string status) =>
        Assert.Null(ResultCheck.ProblemWith($$"""{"status":"{{status}}","summary":"s"}"""));

    [Fact]
    public void Text_that_is_not_json_is_a_problem() =>
        Assert.Contains("not valid JSON", ResultCheck.ProblemWith("{oops"));

    [Theory]
    [InlineData("""{"summary":"s"}""")]
    [InlineData("""{"status":"finished"}""")]
    [InlineData("""{"status":"DONE"}""")]
    public void A_missing_or_unknown_status_is_a_problem_naming_the_allowed_ones(string json)
    {
        var problem = ResultCheck.ProblemWith(json);
        Assert.Contains("status", problem);
        Assert.Contains("plan_ready", problem);
    }

    [Fact]
    public void A_json_value_that_is_not_an_object_is_a_problem() =>
        Assert.NotNull(ResultCheck.ProblemWith("[1]"));
}
