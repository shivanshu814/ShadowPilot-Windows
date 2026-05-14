using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ShadowPilot.Controls;

// Renders markdown text as a WPF FlowDocument inside a RichTextBox
public sealed class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(MarkdownViewer),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MarkdownViewer()
    {
        IsReadOnly    = true;
        BorderThickness = new Thickness(0);
        Background    = Brushes.Transparent;
        Foreground    = new SolidColorBrush(Color.FromArgb(217, 255, 255, 255));
        FontSize      = 13;
        FontFamily    = new FontFamily("Segoe UI, Consolas");
        IsDocumentEnabled = true;
        Focusable     = false;
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
            viewer.Render((string)e.NewValue ?? "");
    }

    private void Render(string markdown)
    {
        var doc   = Markdig.Markdown.Parse(markdown, Pipeline);
        var flow  = new FlowDocument { PagePadding = new Thickness(0) };

        foreach (var block in doc)
            flow.Blocks.Add(ConvertBlock(block));

        Document = flow;
    }

    private static Block ConvertBlock(Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case HeadingBlock hb:
            {
                var para = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
                var size = hb.Level switch { 1 => 20.0, 2 => 16.0, 3 => 14.0, _ => 13.0 };
                para.FontSize   = size;
                para.FontWeight = FontWeights.Bold;
                para.Foreground = AmberBrush;
                AddInlines(para.Inlines, hb.Inline);
                return para;
            }

            case FencedCodeBlock fcb:
            {
                var code = fcb.Lines.ToString();
                var para = new Paragraph { Margin = new Thickness(0, 6, 0, 6) };
                var run  = new Run(code)
                {
                    FontFamily  = new FontFamily("Consolas, Courier New"),
                    FontSize    = 12,
                    Foreground  = new SolidColorBrush(Color.FromRgb(116, 199, 157)),
                    Background  = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                };
                para.Inlines.Add(run);
                para.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                para.Padding    = new Thickness(10, 6, 10, 6);
                return para;
            }

            case ListBlock lb:
            {
                var list = new List { MarkerStyle = lb.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                                      Margin      = new Thickness(0, 2, 0, 2),
                                      Padding     = new Thickness(20, 0, 0, 0) };
                foreach (var item in lb.OfType<ListItemBlock>())
                {
                    var li = new ListItem();
                    foreach (var child in item) li.Blocks.Add(ConvertBlock(child));
                    list.ListItems.Add(li);
                }
                return list;
            }

            case QuoteBlock qb:
            {
                var section = new Section { Margin = new Thickness(8, 4, 0, 4) };
                section.BorderBrush     = AmberBrush;
                section.BorderThickness = new Thickness(3, 0, 0, 0);
                section.Padding         = new Thickness(8, 0, 0, 0);
                foreach (var child in qb) section.Blocks.Add(ConvertBlock(child));
                return section;
            }

            case ParagraphBlock pb:
            {
                var para = new Paragraph { Margin = new Thickness(0, 3, 0, 3) };
                AddInlines(para.Inlines, pb.Inline);
                return para;
            }

            case ThematicBreakBlock:
            {
                var para = new Paragraph(new Run("─────────────────────────"))
                {
                    Foreground = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    Margin     = new Thickness(0, 4, 0, 4),
                };
                return para;
            }

            default:
            {
                var para = new Paragraph();
                para.Inlines.Add(new Run(block.ToString() ?? ""));
                return para;
            }
        }
    }

    private static void AddInlines(InlineCollection target, Markdig.Syntax.Inlines.ContainerInline? container)
    {
        if (container == null) return;
        foreach (var inline in container)
            target.AddRange(ConvertInline(inline));
    }

    private static IEnumerable<Inline> ConvertInline(Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                yield return new Run(lit.Content.ToString());
                break;

            case EmphasisInline em:
            {
                var span = new Span();
                if (em.DelimiterCount == 2) span.FontWeight = FontWeights.Bold;
                else span.FontStyle = FontStyles.Italic;
                AddInlines(span.Inlines, em);
                yield return span;
                break;
            }

            case CodeInline code:
            {
                var run = new Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(116, 199, 157)),
                    Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                };
                yield return run;
                break;
            }

            case LinkInline link:
            {
                var hl = new Hyperlink { Foreground = BlueBrush };
                AddInlines(hl.Inlines, link);
                yield return hl;
                break;
            }

            case LineBreakInline:
                yield return new LineBreak();
                break;

            case ContainerInline container:
            {
                foreach (var child in container)
                    foreach (var converted in ConvertInline(child))
                        yield return converted;
                break;
            }

            default:
                yield return new Run(inline.ToString() ?? "");
                break;
        }
    }

    private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(252, 194, 72));
    private static readonly SolidColorBrush BlueBrush  = new(Color.FromRgb(115, 199, 255));
}
