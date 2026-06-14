using DesktopMascot.Agent.Tools;
using System.Text.Json;

namespace DesktopMascot.Agent.Tests;

public class HtmlExtractorStreamingTests
{
    [Fact]
    public void HtmlContentExtractor_ExtractText_ShouldRemoveTags()
    {
        var html = "<html><head><title>Test</title></head><body><p>Hello World</p></body></html>";
        var text = HtmlContentExtractor.ExtractText(html);

        Assert.Contains("Hello World", text);
        Assert.DoesNotContain("<p>", text);
        Assert.DoesNotContain("</p>", text);
    }

    [Fact]
    public void HtmlContentExtractor_ExtractKeyContent_ShouldExtractTitle()
    {
        var html = "<html><head><title>My Page</title></head><body><h1>Main Heading</h1><p>Content here</p></body></html>";
        var content = HtmlContentExtractor.ExtractKeyContent(html);

        Assert.Contains("My Page", content);
        Assert.Contains("Main Heading", content);
    }

    [Fact]
    public void HtmlContentExtractor_ExtractText_ShouldHandleEmpty()
    {
        Assert.Equal("", HtmlContentExtractor.ExtractText(""));
        Assert.Equal("", HtmlContentExtractor.ExtractText("   "));
    }

    [Fact]
    public void HtmlContentExtractor_ExtractText_ShouldRemoveScripts()
    {
        var html = "<p>Hello</p><script>alert('xss')</script><p>World</p>";
        var text = HtmlContentExtractor.ExtractText(html);

        Assert.Contains("Hello", text);
        Assert.Contains("World", text);
        Assert.DoesNotContain("alert", text);
    }

    [Fact]
    public void HtmlContentExtractor_ExtractText_ShouldTruncateLongContent()
    {
        var html = "<p>" + new string('A', 20000) + "</p>";
        var text = HtmlContentExtractor.ExtractText(html, maxLength: 5000);

        Assert.True(text.Length <= 5100);
        Assert.Contains("截断", text);
    }

    [Fact]
    public void HtmlContentExtractor_ExtractKeyContent_ShouldExtractHeadings()
    {
        var html = "<h1>Title 1</h1><h2>Title 2</h2><h3>Title 3</h3>";
        var content = HtmlContentExtractor.ExtractKeyContent(html);

        Assert.Contains("# Title 1", content);
        Assert.Contains("## Title 2", content);
        Assert.Contains("### Title 3", content);
    }

    [Fact]
    public void HtmlContentExtractor_ExtractKeyContent_ShouldExtractListItems()
    {
        var html = "<ul><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul>";
        var content = HtmlContentExtractor.ExtractKeyContent(html);

        Assert.Contains("• Item 1", content);
        Assert.Contains("• Item 2", content);
        Assert.Contains("• Item 3", content);
    }
}
