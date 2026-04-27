using Markdig;

namespace BlePositioning.Admin.Services;

public sealed class CustomerShowcaseMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string ToHtml(string markdown) =>
        Markdown.ToHtml(markdown, Pipeline);
}
