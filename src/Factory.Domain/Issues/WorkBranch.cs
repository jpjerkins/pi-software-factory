using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Factory.Domain.Issues;

/// <summary>
/// The branch an issue is worked on: <c>&lt;lane&gt;/&lt;number&gt;-&lt;first title words&gt;</c>.
/// The slug doubles as the worktree folder name.
/// </summary>
public sealed partial record WorkBranch(string Lane, string Slug)
{
    private const int MaxTitleWords = 4;
    private const int MaxSlugLength = 40;

    public string BranchName => $"{Lane}/{Slug}";

    public static WorkBranch For(Issue issue)
    {
        var lane = issue.Lane
            ?? throw new InvalidOperationException($"Issue {issue.Number} needs exactly one lane:* label to have a branch.");
        return new WorkBranch(lane, SlugFor(issue));
    }

    private static string SlugFor(Issue issue)
    {
        var slug = issue.Number.Value.ToString(CultureInfo.InvariantCulture);
        foreach (var word in TitleWords(issue.Title).Take(MaxTitleWords))
        {
            if (slug.Length + 1 + word.Length > MaxSlugLength)
            {
                break;
            }

            slug += "-" + word;
        }

        return slug;
    }

    private static IEnumerable<string> TitleWords(string title)
    {
        var ascii = new string([.. title.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
        return NonWord().Split(ascii.ToLowerInvariant()).Where(w => w.Length > 0);
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();
}
