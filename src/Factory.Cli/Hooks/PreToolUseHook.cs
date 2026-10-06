using System.Text.Json;
using Factory.Adapters.Storage;
using Factory.Domain.Guard;

namespace Factory.Cli.Hooks;

/// <summary>Guards every worker tool call. Fails closed: any problem denies and exits 2.</summary>
public static class PreToolUseHook
{
    public static int Run(HookIo io)
    {
        try
        {
            var environment = HookEnvironment.Read(io.Env);
            var input = HookInput.Parse(io.Stdin.ReadToEnd());
            var tool = input.RequiredText("tool_name");
            var context = new GuardContext(environment.Worktree, environment.RunDir, environment.Home, input.Text("cwd"));

            var decision = ToolGuard.Decide(tool, input.ToolInput, context);
            Record(new RunLog(environment.RunDir, io.Now), tool, decision);
            if (!decision.IsAllowed)
            {
                io.Stdout.Write(DenyJson(decision.Reason));
            }

            return 0;
        }
        catch (Exception e)
        {
            var reason = $"The factory guard could not check this call, so it is denied: {e.Message}";
            io.Stdout.Write(DenyJson(reason));
            io.Stderr.WriteLine(reason);
            return 2;
        }
    }

    private static void Record(RunLog log, string tool, GuardDecision decision)
    {
        if (decision.IsAllowed)
        {
            log.Activity("pre-tool-use", tool, "allow");
            return;
        }

        log.Append(RunFiles.Denials, ("event", "pre-tool-use"), ("tool", tool), ("reason", decision.Reason));
        log.Activity("pre-tool-use", tool, "deny", decision.Reason);
    }

    private static string DenyJson(string reason) =>
        JsonSerializer.Serialize(new
        {
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = "deny",
                permissionDecisionReason = reason,
            },
        });
}
