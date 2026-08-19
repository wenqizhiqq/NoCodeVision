using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NoCodeVision.Views.Controls;

public partial class AppleIconTile : UserControl
{
    public AppleIconTile()
    {
        InitializeComponent();
        ApplySize();
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(AppleIconTile),
            new PropertyMetadata(""));

    public static readonly DependencyProperty TileSizeProperty =
        DependencyProperty.Register(nameof(TileSize), typeof(double), typeof(AppleIconTile),
            new PropertyMetadata(28.0, OnSizeChanged));

    public static readonly DependencyProperty TileBrushProperty =
        DependencyProperty.Register(nameof(TileBrush), typeof(Brush), typeof(AppleIconTile),
            new PropertyMetadata(null, OnBrushChanged));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double TileSize
    {
        get => (double)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    public Brush TileBrush
    {
        get => (Brush)GetValue(TileBrushProperty);
        set => SetValue(TileBrushProperty, value);
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AppleIconTile)d).ApplySize();

    private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AppleIconTile)d).ApplyBrush();

    private void ApplySize()
    {
        var s = TileSize;
        Tile.Width = s;
        Tile.Height = s;
        Tile.CornerRadius = new CornerRadius(s * 0.22);
        GlyphText.FontSize = s * 0.56;
    }

    private void ApplyBrush()
    {
        Tile.Background = TileBrush ?? new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEF));
    }
}
