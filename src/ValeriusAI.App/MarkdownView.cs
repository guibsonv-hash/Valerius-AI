using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
namespace ValeriusAI.App;

// Renderização nativa sem WebView, execução de HTML ou acesso a imagens remotas.
public sealed class MarkdownView : StackPanel
{
    public MarkdownView() {Spacing=12;}
    public void Render(string text)
    {
        Children.Clear();
        foreach(var block in Markdown.Parse(text)) AddBlock(block,this);
    }
    private static SelectableTextBlock Selectable(string text)=>new(){Text=text,TextWrapping=TextWrapping.Wrap,FontSize=15,LineHeight=24};
    private static void AddBlock(Block block,StackPanel parent)
    {
        if(block is CodeBlock code)
        {
            var value=code.Lines.ToString();var panel=new StackPanel{Spacing=8};
            var label=Design.Text(code is FencedCodeBlock fenced?fenced.Info??"código":"código",12);
            var copy=Design.Button("Copiar código");copy.HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right;
            copy.Click+=async(_,_)=>{if(TopLevel.GetTopLevel(copy)?.Clipboard is {} clipboard)await clipboard.SetTextAsync(value);};
            panel.Children.Add(label);panel.Children.Add(new ScrollViewer{HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,Content=new SelectableTextBlock{Text=value,FontFamily=new FontFamily("Menlo, Consolas, monospace"),FontSize=13,LineHeight=21}});panel.Children.Add(copy);
            var border=new Border{Padding=new Thickness(16),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),Child=panel};border.Bind(Border.BackgroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Sidebar"));border.Bind(Border.BorderBrushProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Line"));parent.Children.Add(border);
        }
        else if(block is LeafBlock leaf)
        {
            var text=Selectable("");if(block is HeadingBlock h){text.FontSize=26-h.Level*2;text.FontWeight=FontWeight.SemiBold;text.LineHeight=text.FontSize+10;}
            if(leaf.Inline is {} inline) foreach(var child in inline) AddInline(child,text.Inlines!);
            else text.Text=leaf.Lines.ToString();
            parent.Children.Add(text);
        }
        else if(block is ListBlock list)
        {
            var index=1;
            foreach(var item in list)
            {
                var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("28,*")};var bullet=Selectable(list.IsOrdered?$"{index++}.":"•");grid.Children.Add(bullet);
                var content=new StackPanel{Spacing=6};Grid.SetColumn(content,1);grid.Children.Add(content);
                if(item is ContainerBlock cb)foreach(var child in cb)AddBlock(child,content);parent.Children.Add(grid);
            }
        }
        else if(block is ContainerBlock container)
        { var content=new StackPanel{Spacing=8,Margin=new Thickness(14,0,0,0)};foreach(var child in container)AddBlock(child,content);parent.Children.Add(content); }
        else if(block is ThematicBreakBlock)parent.Children.Add(new Separator());
    }
    private static void AddInline(Markdig.Syntax.Inlines.Inline item,InlineCollection target)
    {
        switch(item)
        {
            case LiteralInline literal:target.Add(new Run(literal.Content.ToString()));break;
            case CodeInline code:target.Add(new Run(code.Content){FontFamily=new FontFamily("Menlo, Consolas, monospace"),FontWeight=FontWeight.Medium});break;
            case LineBreakInline:target.Add(new Run("\n"));break;
            case EmphasisInline emphasis:
                var span=new Span();if(emphasis.DelimiterCount==2)span.FontWeight=FontWeight.Bold;else span.FontStyle=FontStyle.Italic;
                foreach(var child in emphasis)AddInline(child,span.Inlines);target.Add(span);break;
            case LinkInline link:
                foreach(var child in link)AddInline(child,target);
                if(!string.IsNullOrWhiteSpace(link.Url))target.Add(new Run($" ({link.Url})"));break;
            case ContainerInline container:foreach(var child in container)AddInline(child,target);break;
        }
    }
}
