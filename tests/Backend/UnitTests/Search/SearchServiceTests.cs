using FluentAssertions;
using Threadia.Modules.Search.Application;
using Xunit;

namespace Threadia.UnitTests.Search;

public class SearchServiceTests
{
    [Fact]
    public void EscapeLike_ワイルドカードをエスケープする()
    {
        SearchService.EscapeLike("100%_done\\x").Should().Be(@"100\%\_done\\x");
    }

    [Fact]
    public void BuildSnippet_短い本文はそのまま返す()
    {
        SearchService.BuildSnippet("こんにちは", "こんに").Should().Be("こんにちは");
    }

    [Fact]
    public void BuildSnippet_長い本文は一致箇所周辺を切り出し省略記号を付ける()
    {
        var content = new string('あ', 300) + "キーワード" + new string('い', 300);

        var snippet = SearchService.BuildSnippet(content, "キーワード");

        snippet.Should().Contain("キーワード");
        snippet.Should().StartWith("…");
        snippet.Should().EndWith("…");
        snippet.Length.Should().BeLessThan(150);
    }
}
