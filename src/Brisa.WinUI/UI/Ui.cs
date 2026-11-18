using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
namespace Brisa.WinUI;
internal static class Ui
{
    public const double DockTileHeight = 64;
    public static bool HighContrast { get; set; }
    enum ContrastRole { Text, Background, Highlight, HighlightText, Border }
    static readonly List<(WeakReference<SolidColorBrush> Brush, string Original, ContrastRole Role, bool CreatedInContrast)> contrastBrushes = [];
    static string ContrastColor(ContrastRole role) => role switch { ContrastRole.Background => ContrastColors.Background, ContrastRole.Highlight => ContrastColors.Highlight, ContrastRole.HighlightText => ContrastColors.HighlightText, _ => ContrastColors.Foreground };
    static SolidColorBrush Adaptive(string original, ContrastRole role)
    {
        var brush = Brush(HighContrast ? ContrastColor(role) : original);
        if (contrastBrushes.Count > 2048) contrastBrushes.RemoveAll(entry => !entry.Brush.TryGetTarget(out _));
        contrastBrushes.Add((new(brush), original, role, HighContrast)); return brush;
    }
    public static void RefreshContrast()
    {
        contrastBrushes.RemoveAll(entry => !entry.Brush.TryGetTarget(out _));
        foreach (var entry in contrastBrushes) if (entry.Brush.TryGetTarget(out var brush)) brush.Color = Color(HighContrast ? ContrastColor(entry.Role) : !entry.CreatedInContrast ? entry.Original : entry.Role switch { ContrastRole.Background => Surface, ContrastRole.Highlight => Light ? "#326E62" : "#C1DACF", ContrastRole.HighlightText => Light ? "#FFFFFF" : "#162D29", ContrastRole.Border => Light ? "#10000000" : "#10FFFFFF", _ => TextColor });
    }
    public static bool Light { get; set; }
    public static bool Midnight { get; set; }
    public static bool Translucent { get; set; } = true;
    public static double TintStrength { get; set; } = .3;
    public static string BaseColor => HighContrast ? ContrastColors.Background : Light ? "#EEF3FA" : Midnight ? "#111722" : "#26364A";
    public static bool ReducedMotion { get; set; }
    public static Color Color(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length == 8 ? Windows.UI.Color.FromArgb(Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16), Convert.ToByte(hex[6..8], 16)) : Windows.UI.Color.FromArgb(255, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
    }
    public static SolidColorBrush Brush(string hex) => new(Color(hex));
    public static string TextColor => HighContrast ? ContrastColors.Foreground : Light ? "#192234" : "#F1F3FA";
    public static string MutedColor => HighContrast ? ContrastColors.Foreground : Light ? "#59677D" : Translucent ? "#D0D7E5" : "#A8B2C5";
    public static string Surface => HighContrast ? ContrastColors.Background : Light ? "#C8FFFFFF" : Translucent ? "#68202A3A" : Midnight ? "#FF1B2331" : "#FF304155";
    public static string Accent => HighContrast ? ContrastColors.Highlight : Light ? "#326E62" : "#B9E9D6";
    public static string TileSurface => HighContrast ? ContrastColors.Background : Light ? "#60FFFFFF" : "#13FFFFFF";
    public static string PanelBackground => Translucent ? "#70" + BaseColor[1..] : BaseColor;
    public static TextBlock Text(string text, double size = 13, string? color = null, bool bold = false) => new() { Text = text, FontSize = size, Foreground = Adaptive(color ?? TextColor, ContrastRole.Text), FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    public static TextBlock Status(double size = 12, string? color = null)
    {
        var text = Text("", size, color ?? MutedColor); text.Visibility = Visibility.Collapsed;
        text.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) => text.Visibility = string.IsNullOrWhiteSpace(text.Text) ? Visibility.Collapsed : Visibility.Visible); return text;
    }
    public static string Context(string description)
    {
        string value = description.ToLower(System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
        value = System.Text.RegularExpressions.Regex.Replace(value, @"\b(chatgpt|claude|codex|cpu|ram|ia|windows|brisa|open-meteo)\b", m => m.Value switch { "chatgpt" => "ChatGPT", "claude" => "Claude", "codex" => "Codex", "cpu" => "CPU", "ram" => "RAM", "ia" => "IA", "windows" => "Windows", "brisa" => "Brisa", _ => "Open-Meteo" });
        return value.Length == 0 ? "" : char.ToUpper(value[0]) + value[1..];
    }
    public static string Count(int count, string singular, string plural) => $"{count} {(count == 1 ? singular : plural)}";
    public static FontIcon Glyph(string code, double size = 22, string? color = null) => new() { Glyph = code, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = size, Foreground = Adaptive(color ?? TextColor, ContrastRole.Text) };
    public static StackPanel Stack(double spacing = 8, bool horizontal = false) => new() { Spacing = spacing, Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
    public static Button Button(object content, string name, Action action, bool accent = false)
    {
        string foreground = accent ? (Light ? "#FFFFFF" : "#162D29") : TextColor;
        object display = content is string label ? new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxLines = 3, Foreground = Adaptive(foreground, accent ? ContrastRole.HighlightText : ContrastRole.Text) } : content;
        var button = new Button { MinWidth = 0, Content = display, HorizontalContentAlignment = content is string ? HorizontalAlignment.Stretch : HorizontalAlignment.Center, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 11, 14, 11), Background = Adaptive(accent ? (Light ? "#326E62" : "#C1DACF") : Surface, accent ? ContrastRole.Highlight : ContrastRole.Background), Foreground = Adaptive(foreground, accent ? ContrastRole.HighlightText : ContrastRole.Text), BorderThickness = new Thickness(1), BorderBrush = Adaptive(Light ? "#10000000" : "#10FFFFFF", ContrastRole.Border), HorizontalAlignment = HorizontalAlignment.Stretch };
        button.IsEnabledChanged += (_, _) => button.Opacity = button.IsEnabled ? 1 : .45;
        AutomationProperties.SetName(button, name); ToolTipService.SetToolTip(button, name); button.Click += (_, _) => action(); return button;
    }
    public static Border Card(UIElement child, double padding = 16) => new() { Child = child, Padding = new Thickness(padding), CornerRadius = new CornerRadius(16), Background = Adaptive(Surface, ContrastRole.Background), BorderBrush = Adaptive(Light ? "#10000000" : "#10FFFFFF", ContrastRole.Border), BorderThickness = new Thickness(1) };
    public static Button Navigation(string title, string detail, string glyph, Action action)
    {
        var row = new Grid { ColumnSpacing = 12 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(new Border { Child = Glyph(glyph, 19, Accent), Width = 38, Height = 38, CornerRadius = new(11), Background = Brush(Light ? "#E6F1EC" : "#203F766A") });
        var text = Stack(3); text.Children.Add(Text(title, 14, bold: true)); text.Children.Add(Text(detail, 11, MutedColor)); Grid.SetColumn(text, 1); row.Children.Add(text);
        var arrow = Glyph("\uE76C", 11, MutedColor); Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
        var button = Button(row, title, action); button.HorizontalContentAlignment = HorizontalAlignment.Stretch; ToolTipService.SetToolTip(button, null); AutomationProperties.SetHelpText(button, detail); return button;
    }
    public static Storyboard Animate(DependencyObject target, string property, double to, int milliseconds = 180)
    {
        var animation = new DoubleAnimation { To = to, Duration = new Duration(TimeSpan.FromMilliseconds(ReducedMotion ? 0 : milliseconds)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, EnableDependentAnimation = true };
        Storyboard.SetTarget(animation, target); Storyboard.SetTargetProperty(animation, property); var storyboard = new Storyboard(); storyboard.Children.Add(animation); storyboard.Begin(); return storyboard;
    }
    public static void Enter(FrameworkElement element, double fromY = 7)
    {
        if (ReducedMotion) return;
        element.Opacity = 0; element.RenderTransform = new TranslateTransform { Y = fromY };
        Animate(element, "Opacity", 1, 150); Animate((TranslateTransform)element.RenderTransform, "Y", 0, 190);
    }
}
