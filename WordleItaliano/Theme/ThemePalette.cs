using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Theme;

public sealed class ThemePalette
{
    private static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;
    private static readonly SolidColorBrush WhiteBrush = Brushes.White;
    private static readonly SolidColorBrush ErrorBrush = Brush(201, 58, 58);
    private static readonly SolidColorBrush OverlayBrush = new(Color.FromArgb(153, 0, 0, 0));
    private static readonly SolidColorBrush PerfectShotBrush = Brush(201, 180, 88);

    private static readonly ThemePalette Original = new(
        Background: Brush(248, 248, 248),
        Foreground: Brush(28, 28, 30),
        Panel: WhiteBrush,
        Border: Brush(225, 225, 225),
        Muted: Brush(92, 92, 96),
        Accent: Brush(83, 141, 78),
        AccentHover: Brush(106, 170, 100),
        AccentForeground: WhiteBrush,
        CalendarHover: Brush(232, 242, 231),
        CalendarNav: Brush(245, 245, 245),
        CalendarOutside: Brush(142, 142, 146),
        TileCorrect: Brush(83, 141, 78),
        TilePresent: Brush(181, 159, 59),
        TileAbsent: Brush(120, 124, 126),
        TileFilled: WhiteBrush,
        TileEmpty: WhiteBrush,
        TileEmptyBorder: Brush(211, 214, 218),
        TileFilledBorder: Brush(135, 138, 140),
        TileTextOnOpenTile: Brush(18, 18, 19));

    private static readonly ThemePalette IbpLight = new(
        Background: Brush(243, 247, 248),
        Foreground: Brush(37, 48, 52),
        Panel: WhiteBrush,
        Border: Brush(201, 211, 214),
        Muted: Brush(91, 105, 110),
        Accent: Brush(0, 116, 122),
        AccentHover: Brush(0, 160, 166),
        AccentForeground: WhiteBrush,
        CalendarHover: Brush(217, 241, 242),
        CalendarNav: Brush(229, 238, 240),
        CalendarOutside: Brush(135, 150, 154),
        TileCorrect: Brush(0, 116, 122),
        TilePresent: Brush(194, 158, 61),
        TileAbsent: Brush(162, 35, 39),
        TileFilled: WhiteBrush,
        TileEmpty: Brush(229, 238, 240),
        TileEmptyBorder: Brush(88, 106, 113),
        TileFilledBorder: Brush(0, 116, 122),
        TileTextOnOpenTile: Brush(18, 32, 36));

    private static readonly ThemePalette IbpDark = new(
        Background: Brush(30, 38, 42),
        Foreground: Brush(247, 250, 250),
        Panel: Brush(38, 48, 52),
        Border: Brush(76, 92, 98),
        Muted: Brush(198, 209, 212),
        Accent: Brush(0, 116, 122),
        AccentHover: Brush(0, 160, 166),
        AccentForeground: WhiteBrush,
        CalendarHover: Brush(0, 94, 99),
        CalendarNav: Brush(38, 48, 52),
        CalendarOutside: Brush(127, 141, 146),
        TileCorrect: Brush(0, 116, 122),
        TilePresent: Brush(194, 158, 61),
        TileAbsent: Brush(162, 35, 39),
        TileFilled: Brush(43, 55, 60),
        TileEmpty: Brush(54, 67, 72),
        TileEmptyBorder: Brush(88, 106, 113),
        TileFilledBorder: Brush(0, 116, 122),
        TileTextOnOpenTile: WhiteBrush);

    private ThemePalette(
        Brush Background,
        Brush Foreground,
        Brush Panel,
        Brush Border,
        Brush Muted,
        Brush Accent,
        Brush AccentHover,
        Brush AccentForeground,
        Brush CalendarHover,
        Brush CalendarNav,
        Brush CalendarOutside,
        Brush TileCorrect,
        Brush TilePresent,
        Brush TileAbsent,
        Brush TileFilled,
        Brush TileEmpty,
        Brush TileEmptyBorder,
        Brush TileFilledBorder,
        Brush TileTextOnOpenTile)
    {
        this.Background = Background;
        this.Foreground = Foreground;
        this.Panel = Panel;
        this.Border = Border;
        this.Muted = Muted;
        this.Accent = Accent;
        this.AccentHover = AccentHover;
        this.AccentForeground = AccentForeground;
        this.CalendarHover = CalendarHover;
        this.CalendarNav = CalendarNav;
        this.CalendarOutside = CalendarOutside;
        this.TileCorrect = TileCorrect;
        this.TilePresent = TilePresent;
        this.TileAbsent = TileAbsent;
        this.TileFilled = TileFilled;
        this.TileEmpty = TileEmpty;
        this.TileEmptyBorder = TileEmptyBorder;
        this.TileFilledBorder = TileFilledBorder;
        this.TileTextOnOpenTile = TileTextOnOpenTile;
    }

    public Brush Background { get; }
    public Brush Foreground { get; }
    public Brush Panel { get; }
    public Brush Border { get; }
    public Brush Muted { get; }
    public Brush Accent { get; }
    public Brush AccentHover { get; }
    public Brush AccentForeground { get; }
    public Brush CalendarHover { get; }
    public Brush CalendarNav { get; }
    public Brush CalendarOutside { get; }
    public Brush TileCorrect { get; }
    public Brush TilePresent { get; }
    public Brush TileAbsent { get; }
    public Brush TileFilled { get; }
    public Brush TileEmpty { get; }
    public Brush TileEmptyBorder { get; }
    public Brush TileFilledBorder { get; }
    public Brush TileTextOnOpenTile { get; }

    public static Brush Error => ErrorBrush;
    public static Brush Overlay => OverlayBrush;
    public static Brush PerfectShot => PerfectShotBrush;

    public static ThemePalette For(string? theme) => ThemeModes.Normalize(theme) switch
    {
        ThemeModes.IbpLight => IbpLight,
        ThemeModes.Original => Original,
        _ => IbpDark
    };

    public Brush GetThemeBrush(string? key) => key switch
    {
        "Background" => Background,
        "Foreground" => Foreground,
        "Panel" => Panel,
        "Border" => Border,
        "Muted" => Muted,
        "Accent" => Accent,
        "AccentHover" => AccentHover,
        "AccentForeground" => AccentForeground,
        "CalendarDay" => TransparentBrush,
        "CalendarHover" => CalendarHover,
        "CalendarNav" => CalendarNav,
        "CalendarOutside" => CalendarOutside,
        "Error" => Error,
        "Overlay" => Overlay,
        "PerfectShot" => PerfectShot,
        _ => TransparentBrush
    };

    public Brush GetTileBrush(TileState state) => state switch
    {
        TileState.Correct => TileCorrect,
        TileState.Present => TilePresent,
        TileState.Absent => TileAbsent,
        TileState.Filled => TileFilled,
        _ => TileEmpty
    };

    public Brush GetTileBorderBrush(TileState state) => state switch
    {
        TileState.Empty => TileEmptyBorder,
        TileState.Filled => TileFilledBorder,
        _ => TransparentBrush
    };

    public Brush GetTileForegroundBrush(TileState state) => state is TileState.Empty or TileState.Filled
        ? TileTextOnOpenTile
        : WhiteBrush;

    private static SolidColorBrush Brush(byte red, byte green, byte blue) =>
        new(Color.FromRgb(red, green, blue));
}
