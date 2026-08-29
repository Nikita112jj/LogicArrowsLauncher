using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace LogicArrowsLauncher.Linux;

/// <summary>Тёмная тема лаунчера — палитра из LauncherForm.cs (Windows-версия).</summary>
public static class LaTheme
{
    public static readonly Color WindowBack = Color.FromRgb(13, 17, 23);      // 0d1117
    public static readonly Color PanelBack = Color.FromRgb(22, 27, 34);       // 161b22
    public static readonly Color Border = Color.FromRgb(48, 54, 61);          // 30363d
    public static readonly Color ButtonBack = Color.FromRgb(33, 38, 45);      // 21262d
    public static readonly Color TextPrimary = Color.FromRgb(240, 246, 252);  // f0f6fc
    public static readonly Color TextSecondary = Color.FromRgb(139, 148, 158);// 8b949e
    public static readonly Color TextBright = Color.FromRgb(201, 209, 217);   // c9d1d9
    public static readonly Color Accent = Color.FromRgb(88, 166, 255);        // 58a6ff
    public static readonly Color AccentStrong = Color.FromRgb(31, 111, 235);  // 1f6feb
    public static readonly Color Success = Color.FromRgb(35, 134, 54);        // 238636
    public static readonly Color SuccessHover = Color.FromRgb(46, 160, 67);   // 2ea043
    public static readonly Color Error = Color.FromRgb(248, 81, 73);          // f84849
}

public sealed class App : Application
{
    public static MainWindow? MainWindowInstance { get; set; }

    public override void Initialize()
    {
        var borderBrush = new SolidColorBrush(LaTheme.Border);

        Styles.Add(ButtonStyle("hdr", new SolidColorBrush(LaTheme.ButtonBack), new SolidColorBrush(LaTheme.TextBright),
            borderBrush, borderBrush));

        var play = ButtonStyle("play", new SolidColorBrush(LaTheme.Success), Brushes.White,
            border: null, hover: new SolidColorBrush(LaTheme.SuccessHover));
        play.Setters.Add(new Setter(Button.FontWeightProperty, FontWeight.Bold));
        play.Setters.Add(new Setter(Button.FontSizeProperty, 17d));
        Styles.Add(play);

        var accent = ButtonStyle("accent", new SolidColorBrush(LaTheme.AccentStrong), Brushes.White,
            border: null, hover: new SolidColorBrush(Color.FromRgb(56, 139, 253)));
        accent.Setters.Add(new Setter(Button.FontWeightProperty, FontWeight.Bold));
        Styles.Add(accent);

        var link = ButtonStyle("link", Brushes.Transparent, new SolidColorBrush(LaTheme.Accent),
            border: null, hover: new SolidColorBrush(Color.FromRgb(121, 192, 255)));
        link.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
        Styles.Add(link);
    }

    private static Style ButtonStyle(string className, IBrush back, IBrush foreground, IBrush? border, IBrush hover)
    {
        var style = new Style(x => x.OfType<Button>().Class(className));
        style.Setters.Add(new Setter(Button.BackgroundProperty, back));
        style.Setters.Add(new Setter(Button.ForegroundProperty, foreground));
        style.Setters.Add(new Setter(Button.CornerRadiusProperty, new CornerRadius(6)));
        style.Setters.Add(new Setter(Button.HeightProperty, 34d));
        if (border is not null)
        {
            style.Setters.Add(new Setter(Button.BorderBrushProperty, border));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
        }
        style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(14, 6, 14, 6)));

        var hoverStyle = new Style(x => x.OfType<Button>().Class(className).Class(":pointerover"));
        hoverStyle.Setters.Add(new Setter(Button.BackgroundProperty, hover));
        return style;
    }
}
