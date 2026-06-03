using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using FormsScreen = System.Windows.Forms.Screen;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Utils;

namespace UniversalBatteryOverlay;

public partial class OverlayWindow : Window
{
    public static readonly DependencyProperty RowPaddingProperty = DependencyProperty.Register(
        nameof(RowPadding), typeof(Thickness), typeof(OverlayWindow), new PropertyMetadata(new Thickness(7, 2, 7, 2)));

    public static readonly DependencyProperty RowMarginProperty = DependencyProperty.Register(
        nameof(RowMargin), typeof(Thickness), typeof(OverlayWindow), new PropertyMetadata(new Thickness(0, 1, 0, 1)));

    public static readonly DependencyProperty RowMinWidthProperty = DependencyProperty.Register(
        nameof(RowMinWidth), typeof(double), typeof(OverlayWindow), new PropertyMetadata(112.0));

    public Thickness RowPadding
    {
        get => (Thickness)GetValue(RowPaddingProperty);
        set => SetValue(RowPaddingProperty, value);
    }

    public Thickness RowMargin
    {
        get => (Thickness)GetValue(RowMarginProperty);
        set => SetValue(RowMarginProperty, value);
    }

    public double RowMinWidth
    {
        get => (double)GetValue(RowMinWidthProperty);
        set => SetValue(RowMinWidthProperty, value);
    }

    private AppSettings? _settings;
    private readonly DropShadowEffect _shadowEffect = new() { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.34 };

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindowStyles();
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        Opacity = settings.OverlayOpacity;
        Items.FontSize = settings.FontSize;
        RowPadding = new Thickness(settings.OverlayRowPaddingX, settings.OverlayRowPaddingY, settings.OverlayRowPaddingX, settings.OverlayRowPaddingY);
        RowMargin = new Thickness(0, settings.OverlayRowSpacing, 0, settings.OverlayRowSpacing);
        RowMinWidth = settings.OverlayMinWidth;

        RootBorder.Padding = new Thickness(settings.OverlayPadding);
        RootBorder.CornerRadius = new CornerRadius(settings.OverlayCornerRadius);
        RootBorder.BorderThickness = settings.ShowOverlayBackground ? new Thickness(1) : new Thickness(0);
        RootBorder.BorderBrush = new SolidColorBrush(ParseColor(settings.OverlayBorderColor, MediaColor.FromRgb(51, 65, 85)));
        RootBorder.Background = settings.ShowOverlayBackground
            ? new SolidColorBrush(WithAlpha(ParseColor(settings.OverlayBackgroundColor, MediaColor.FromRgb(11, 16, 32)), settings.OverlayBackgroundOpacity))
            : MediaBrushes.Transparent;
        Resources["OverlayRowBrush"] = new SolidColorBrush(WithAlpha(ParseColor(settings.OverlayRowBackgroundColor, MediaColor.FromRgb(17, 26, 46)), Math.Min(0.95, settings.OverlayBackgroundOpacity + 0.12)));
        Resources["OverlayBorderBrush"] = new SolidColorBrush(ParseColor(settings.OverlayBorderColor, MediaColor.FromRgb(51, 65, 85)));
        Resources["OverlayTextBrush"] = new SolidColorBrush(ParseColor(settings.OverlayTextColor, MediaColor.FromRgb(248, 250, 252)));
        Resources["OverlayValueBrush"] = new SolidColorBrush(ParseColor(settings.OverlayValueColor, MediaColor.FromRgb(233, 213, 255)));
        RootBorder.Effect = settings.ShowOverlayShadow ? _shadowEffect : null;

        Topmost = true;
        ApplyWindowStyles();
        Reposition();
    }

    public void UpdateDevices(IReadOnlyList<DeviceBatteryInfo> devices)
    {
        var visibleDevices = devices
            .Where(d => d.BatteryPercent.HasValue)
            .OrderBy(d => DeviceSortOrder(d.DeviceType))
            .ThenBy(d => d.TypeDisplay)
            .ToList();

        Items.ItemsSource = visibleDevices;
        RootBorder.Visibility = visibleDevices.Count == 0 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        Reposition();
    }

    public void Reposition()
    {
        if (_settings is null) return;
        var screen = GetTargetScreen(_settings);
        var work = screen.WorkingArea;

        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        Width = Math.Max(ActualWidth, Math.Max(_settings.OverlayMinWidth + 20, 120));
        UpdateLayout();

        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : 54;
        var leftPx = work.Left;
        var topPx = work.Top;

        switch (_settings.Position)
        {
            case OverlayPosition.TopLeft:
                leftPx = work.Left + _settings.OffsetX;
                topPx = work.Top + _settings.OffsetY;
                break;
            case OverlayPosition.TopRight:
                leftPx = work.Right - (int)(w / scaleX) - _settings.OffsetX;
                topPx = work.Top + _settings.OffsetY;
                break;
            case OverlayPosition.BottomLeft:
                leftPx = work.Left + _settings.OffsetX;
                topPx = work.Bottom - (int)(h / scaleY) - _settings.OffsetY;
                break;
            case OverlayPosition.BottomRight:
                leftPx = work.Right - (int)(w / scaleX) - _settings.OffsetX;
                topPx = work.Bottom - (int)(h / scaleY) - _settings.OffsetY;
                break;
            case OverlayPosition.Custom:
                leftPx = work.Left + _settings.OffsetX;
                topPx = work.Top + _settings.OffsetY;
                break;
        }

        Left = leftPx * scaleX;
        Top = topPx * scaleY;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    private static MediaColor WithAlpha(MediaColor color, double alpha)
    {
        color.A = ToByte(alpha);
        return color;
    }

    private static MediaColor ParseColor(string? text, MediaColor fallback)
    {
        try
        {
            var value = text?.Trim();
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
            return (MediaColor)MediaColorConverter.ConvertFromString(value)!;
        }
        catch
        {
            return fallback;
        }
    }

    private static int DeviceSortOrder(string deviceType) => deviceType.ToLowerInvariant() switch
    {
        "mouse" => 0,
        "keyboard" => 1,
        "headset" => 2,
        "headphones" => 2,
        "laptop" => 3,
        "battery" => 4,
        _ => 9
    };

    private static FormsScreen GetTargetScreen(AppSettings settings)
    {
        if (settings.MonitorDeviceName == "Primary") return FormsScreen.PrimaryScreen ?? FormsScreen.AllScreens.First();
        return FormsScreen.AllScreens.FirstOrDefault(s => s.DeviceName == settings.MonitorDeviceName)
               ?? FormsScreen.PrimaryScreen
               ?? FormsScreen.AllScreens.First();
    }

    private void ApplyWindowStyles()
    {
        if (_settings is null) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            NativeMethods.ApplyOverlayStyles(this, _settings.ClickThrough);
    }
}
