using System.Text;

namespace Factory.Domain.Guard;

/// <summary>
/// A pragmatic splitter, not a shell parser. It breaks a command line into simple commands at
/// <c>&amp;&amp; || ; | &amp;</c>, newlines, <c>$(</c>, backticks and parentheses, and looks inside
/// <c>bash -c '...'</c>. It would rather over-split (a false deny) than miss a command.
/// </summary>
internal static class ShellCommand
{
    private static readonly HashSet<string> Shells = ["bash", "sh", "zsh", "dash"];

    public static IReadOnlyList<SimpleCommand> Parse(string line)
    {
        var commands = new List<SimpleCommand>();
        foreach (var words in Split(line))
        {
            var command = new SimpleCommand(words);
            commands.Add(command);
            if (Shells.Contains(command.Name) && ScriptArgument(command.Args) is { } script)
            {
                commands.AddRange(Parse(script));
            }
        }

        return commands;
    }

    // The argument after a "-c" style flag, such as the script in: bash -lc 'git push'
    private static string? ScriptArgument(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i].StartsWith('-') && !args[i].StartsWith("--") && args[i].Contains('c'))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static List<List<string>> Split(string line)
    {
        var commands = new List<List<string>>();
        var words = new List<string>();
        var word = new StringBuilder();
        var hasWord = false;
        char? quote = null;

        void EndWord()
        {
            if (hasWord)
            {
                words.Add(word.ToString());
            }

            word.Clear();
            hasWord = false;
        }

        void EndCommand()
        {
            EndWord();
            if (words.Count > 0)
            {
                commands.Add(words);
                words = [];
            }
        }

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote == '\'')
            {
                if (c == '\'') { quote = null; } else { word.Append(c); }
            }
            else if (c == '\\' && i + 1 < line.Length)
            {
                word.Append(line[++i]);
                hasWord = true;
            }
            else if (c == '`' || (c == '$' && i + 1 < line.Length && line[i + 1] == '('))
            {
                EndCommand();
                if (c == '$') { i++; }
            }
            else if (quote == '"')
            {
                if (c == '"') { quote = null; } else { word.Append(c); }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                hasWord = true;
            }
            else if (c is ';' or '|' or '&' or '\n' or '(' or ')')
            {
                EndCommand();
            }
            else if (char.IsWhiteSpace(c))
            {
                EndWord();
            }
            else
            {
                word.Append(c);
                hasWord = true;
            }
        }

        EndCommand();
        return commands;
    }
}
