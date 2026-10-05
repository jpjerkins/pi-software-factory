namespace Factory.Adapters.Shell;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
