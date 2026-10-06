namespace Factory.Domain.Guard;

/// <summary>One command from a shell line: its words with quotes removed.</summary>
internal sealed class SimpleCommand
{
    private static readonly HashSet<string> Wrappers =
        ["command", "env", "sudo", "exec", "nohup", "time", "builtin", "nice", "xargs"];

    public SimpleCommand(IReadOnlyList<string> words)
    {
        var i = 0;
        var afterWrapper = false;
        var viaXargs = false;
        while (i < words.Count)
        {
            var word = words[i];
            if (IsAssignment(word) || Wrappers.Contains(word) || (afterWrapper && word.StartsWith('-')))
            {
                viaXargs |= word == "xargs";
                afterWrapper = Wrappers.Contains(word) || (afterWrapper && word.StartsWith('-'));
                i++;
                continue;
            }

            break;
        }

        Words = words;
        Name = i < words.Count ? words[i][(words[i].LastIndexOf('/') + 1)..] : "";
        Args = words.Skip(i + 1).ToList();
        TargetsComeFromInput = viaXargs;
    }

    /// <summary>Every word on the command line, including redirections.</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>The program being run, without directory, after env assignments and wrappers such as <c>command</c>.</summary>
    public string Name { get; }

    public IReadOnlyList<string> Args { get; }

    /// <summary>True for <c>xargs rm</c> and the like: the real operands are not visible.</summary>
    public bool TargetsComeFromInput { get; }

    /// <summary>The arguments that are not options. Everything after <c>--</c> counts.</summary>
    public static IEnumerable<string> OperandsOf(IReadOnlyList<string> args)
    {
        var afterDashes = false;
        foreach (var arg in args)
        {
            if (afterDashes || !arg.StartsWith('-'))
            {
                yield return arg;
            }
            else if (arg == "--")
            {
                afterDashes = true;
            }
        }
    }

    private static bool IsAssignment(string word)
    {
        var eq = word.IndexOf('=');
        return eq > 0 && word[..eq].All(c => char.IsLetterOrDigit(c) || c == '_') && !char.IsDigit(word[0]);
    }
}
