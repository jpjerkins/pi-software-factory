using System.Text.Json;

namespace Factory.Adapters.Storage;

/// <summary>Checks result.json text against the shape workers must write. Shares its schema with <see cref="RunStore"/>.</summary>
public static class ResultCheck
{
    public const string Shape =
        """{"status":"plan_ready|done|needs_input|blocked","summary":"...","tests_run":[],"files_touched":[],"questions":[],"problems_found":[]}""";

    /// <returns>What is wrong with <paramref name="json"/>, or <c>null</c> when it is a valid result.</returns>
    public static string? ProblemWith(string json)
    {
        ResultDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ResultDocument>(json, StoreJson.Options);
        }
        catch (JsonException e)
        {
            return $"result.json is not valid JSON ({e.Message}).";
        }

        return document?.ToResult() is null
            ? $"result.json has a missing or unknown \"status\" (got {document?.Status ?? "none"}); it must be one of plan_ready, done, needs_input, blocked."
            : null;
    }
}
