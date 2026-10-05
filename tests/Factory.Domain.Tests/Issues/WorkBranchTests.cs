using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

public class WorkBranchTests
{
    private static WorkBranch For(string title, int number = 103, string lane = "lane:adapters") =>
        WorkBranch.For(AnIssue.Eligible().Number(number).Titled(title).Labelled("build", "agent:claude", lane).Build());

    [Fact]
    public void Branch_is_lane_slash_slug()
    {
        var branch = For("GitHub sync adapter");
        Assert.Equal("adapters", branch.Lane);
        Assert.Equal("103-github-sync-adapter", branch.Slug);
        Assert.Equal("adapters/103-github-sync-adapter", branch.BranchName);
    }

    [Fact]
    public void Slug_keeps_only_the_first_four_words() =>
        Assert.Equal("103-one-two-three-four", For("One two three four five six").Slug);

    [Fact]
    public void Slug_is_lowercase_kebab_with_punctuation_dropped() =>
        Assert.Equal("103-restore-drill-verify-the", For("Restore drill — verify the Backup!").Slug);

    [Fact]
    public void Slug_is_ascii() =>
        Assert.Equal("5-cafe-creme", For("Café crème", 5).Slug);

    [Fact]
    public void Slug_is_at_most_forty_characters_cut_at_a_word_boundary()
    {
        var slug = For("Internationalization infrastructure configuration refactoring").Slug;
        Assert.True(slug.Length <= 40);
        Assert.Equal("103-internationalization-infrastructure", slug);
    }

    [Fact]
    public void Title_with_no_usable_words_gives_just_the_number() =>
        Assert.Equal("103", For("— !!").Slug);

    [Fact]
    public void Issue_without_exactly_one_lane_cannot_have_a_branch()
    {
        var none = AnIssue.Eligible().WithoutLabel("lane:adapters").Build();
        Assert.Throws<InvalidOperationException>(() => WorkBranch.For(none));
    }
}
