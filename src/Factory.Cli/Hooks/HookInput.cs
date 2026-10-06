using System.Text.Json;

namespace Factory.Cli.Hooks;

/// <summary>The JSON object Claude Code sends a hook on stdin. Unknown fields are ignored.</summary>
public sealed class HookInput(JsonElement root)
{
    public static HookInput Parse(string stdin)
    {
        try
        {
            var root = JsonDocument.Parse(stdin).RootElement.Clone();
            return root.ValueKind == JsonValueKind.Object ? new HookInput(root) : throw new HookFailure("Hook input is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new HookFailure($"Hook input is not valid JSON: {e.Message}");
        }
    }

    public string? Text(string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public string RequiredText(string field) => Text(field) ?? throw new HookFailure($"Hook input has no \"{field}\".");

    public JsonElement ToolInput =>
        root.TryGetProperty("tool_input", out var value) ? value : default;
}
