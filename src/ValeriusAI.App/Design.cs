using Avalonia.Layout;

using Avalonia.Controls.Primitives;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
namespace ValeriusAI.App;
public static class Design
{
    public const double BodySize=15;
    public static Styles CreateStyles()
    {
        var styles=new Styles();
        styles.Resources.ThemeDictionaries[ThemeVariant.Dark]=Palette("#0D1D2A","#0A1722","#132B40","#F3F7F8","#9EB3BD","#23465A","#36C8B5");
        styles.Resources.ThemeDictionaries[ThemeVariant.Light]=Palette("#F5F7F8","#EAF0F2","#FFFFFF","#132B40","#55717D","#CEDDE2","#087F8C");
        styles.Add(new Style(s=>s.OfType<Window>()){Setters={new Setter(TemplatedControl.FontFamilyProperty,new FontFamily("Inter, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif")),new Setter(TemplatedControl.FontSizeProperty,BodySize)}});
        styles.Add(new Style(s=>s.OfType<Button>()){Setters={new Setter(TemplatedControl.CornerRadiusProperty,new CornerRadius(8)),new Setter(TemplatedControl.PaddingProperty,new Thickness(14,9)),new Setter(Layoutable.MinHeightProperty,40d),new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Center),new Setter(ContentControl.VerticalContentAlignmentProperty,VerticalAlignment.Center)}});
        styles.Add(new Style(s=>s.OfType<TextBox>()){Setters={new Setter(TemplatedControl.CornerRadiusProperty,new CornerRadius(8)),new Setter(TemplatedControl.PaddingProperty,new Thickness(12,10)),new Setter(Layoutable.MinHeightProperty,42d),new Setter(TextBox.VerticalContentAlignmentProperty,VerticalAlignment.Center)}});
        styles.Add(new Style(s=>s.OfType<ComboBox>()){Setters={new Setter(Layoutable.MinHeightProperty,42d)}});
        styles.Add(new Style(s=>s.OfType<Button>().Class("primary")){Setters={new Setter(TemplatedControl.BackgroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Accent")),new Setter(TemplatedControl.ForegroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("OnAccent"))}});
        styles.Add(new Style(s=>s.OfType<Button>().Class("nav")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent),new Setter(TemplatedControl.BorderBrushProperty,Brushes.Transparent),new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Left)}});
        styles.Add(new Style(s=>s.OfType<Button>().Class("nav").Class("active")){Setters={new Setter(TemplatedControl.BackgroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Line")),new Setter(TemplatedControl.BorderBrushProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Line"))}});
        styles.Add(new Style(s=>s.OfType<Button>().Class("segment")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent),new Setter(TemplatedControl.BorderBrushProperty,Brushes.Transparent),new Setter(TemplatedControl.CornerRadiusProperty,new CornerRadius(18)),new Setter(TemplatedControl.MinWidthProperty,96d),new Setter(TemplatedControl.MinHeightProperty,38d),new Setter(TemplatedControl.PaddingProperty,new Thickness(18,7))}});
        styles.Add(new Style(s=>s.OfType<Button>().Class("segment").Class("active")){Setters={new Setter(TemplatedControl.BackgroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Surface")),new Setter(TemplatedControl.BorderBrushProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Line"))}});
        styles.Add(new Style(s=>s.OfType<ListBoxItem>()){Setters={new Setter(TemplatedControl.CornerRadiusProperty,new CornerRadius(8)),new Setter(TemplatedControl.PaddingProperty,new Thickness(10,6)),new Setter(Layoutable.MarginProperty,new Thickness(0,2))}});
        return styles;
    }
    private static ResourceDictionary Palette(string bg,string side,string surface,string text,string muted,string line,string accent)=>new()
    {
        ["Page"]=Brush.Parse(bg),["Sidebar"]=Brush.Parse(side),["Surface"]=Brush.Parse(surface),["Ink"]=Brush.Parse(text),["Muted"]=Brush.Parse(muted),["Line"]=Brush.Parse(line),["Accent"]=Brush.Parse(accent),["OnAccent"]=Brush.Parse("#FFFFFF"),["BrandDeep"]=Brush.Parse("#132B40"),["BrandTeal"]=Brush.Parse("#087F8C"),["BrandMint"]=Brush.Parse("#36C8B5"),
        ["ButtonBackground"]=Brush.Parse(surface),["ButtonBackgroundPointerOver"]=Brush.Parse(line),["ButtonBackgroundPressed"]=Brush.Parse(side),
        ["ButtonForeground"]=Brush.Parse(text),["ButtonForegroundPointerOver"]=Brush.Parse(text),["ButtonForegroundPressed"]=Brush.Parse(text),
        ["ButtonBorderBrush"]=Brush.Parse(line),["ButtonBorderBrushPointerOver"]=Brush.Parse(muted),
        ["TextControlBackground"]=Brush.Parse(surface),["TextControlBackgroundFocused"]=Brush.Parse(surface),["TextControlBackgroundPointerOver"]=Brush.Parse(surface),
        ["TextControlForeground"]=Brush.Parse(text),["TextControlForegroundFocused"]=Brush.Parse(text),
        ["TextControlPlaceholderForeground"]=Brush.Parse(muted),["TextControlPlaceholderForegroundFocused"]=Brush.Parse(muted),
        ["SystemControlHighlightListAccentLowBrush"]=Brush.Parse(line),["SystemControlHighlightListAccentMediumBrush"]=Brush.Parse(line),["SystemControlHighlightListAccentHighBrush"]=Brush.Parse(line),
        ["ListBoxItemSelectedBackground"]=Brush.Parse(line),["ListBoxItemSelectedPointerOverBackground"]=Brush.Parse(line),
        ["SystemAccentColor"]=Color.Parse(accent),["SystemAccentColorLight1"]=Color.Parse(accent),["SystemAccentColorDark1"]=Color.Parse(accent)
    };
    public static TextBlock Text(string text,double size=BodySize,bool bold=false)
    {var t=new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeight.SemiBold:FontWeight.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};t.Bind(TextBlock.ForegroundProperty,new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Ink"));return t;}
    public static Button Button(string text,string? help=null)
    {var b=new Button{Content=text,VerticalContentAlignment=VerticalAlignment.Center,HorizontalContentAlignment=HorizontalAlignment.Center};Avalonia.Automation.AutomationProperties.SetName(b,help??text);if(help is not null) ToolTip.SetTip(b,help);return b;}
}
