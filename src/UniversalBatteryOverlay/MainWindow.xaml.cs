using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using FormsScreen = System.Windows.Forms.Screen;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Services;

namespace UniversalBatteryOverlay;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty PreviewRowPaddingProperty = DependencyProperty.Register(
        nameof(PreviewRowPadding), typeof(Thickness), typeof(MainWindow), new PropertyMetadata(new Thickness(7, 2, 7, 2)));

    public static readonly DependencyProperty PreviewRowMarginProperty = DependencyProperty.Register(
        nameof(PreviewRowMargin), typeof(Thickness), typeof(MainWindow), new PropertyMetadata(new Thickness(0, 1, 0, 1)));

    public static readonly DependencyProperty PreviewRowMinWidthProperty = DependencyProperty.Register(
        nameof(PreviewRowMinWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(112.0));

    public static readonly DependencyProperty PreviewRowBackgroundBrushProperty = DependencyProperty.Register(
        nameof(PreviewRowBackgroundBrush), typeof(MediaBrush), typeof(MainWindow), new PropertyMetadata(new SolidColorBrush(MediaColor.FromRgb(17, 26, 46))));

    public static readonly DependencyProperty PreviewBorderBrushProperty = DependencyProperty.Register(
        nameof(PreviewBorderBrush), typeof(MediaBrush), typeof(MainWindow), new PropertyMetadata(new SolidColorBrush(MediaColor.FromRgb(51, 65, 85))));

    public static readonly DependencyProperty PreviewTextBrushProperty = DependencyProperty.Register(
        nameof(PreviewTextBrush), typeof(MediaBrush), typeof(MainWindow), new PropertyMetadata(new SolidColorBrush(MediaColor.FromRgb(248, 250, 252))));

    public static readonly DependencyProperty PreviewValueBrushProperty = DependencyProperty.Register(
        nameof(PreviewValueBrush), typeof(MediaBrush), typeof(MainWindow), new PropertyMetadata(new SolidColorBrush(MediaColor.FromRgb(233, 213, 255))));

    public Thickness PreviewRowPadding
    {
        get => (Thickness)GetValue(PreviewRowPaddingProperty);
        set => SetValue(PreviewRowPaddingProperty, value);
    }

    public Thickness PreviewRowMargin
    {
        get => (Thickness)GetValue(PreviewRowMarginProperty);
        set => SetValue(PreviewRowMarginProperty, value);
    }

    public double PreviewRowMinWidth
    {
        get => (double)GetValue(PreviewRowMinWidthProperty);
        set => SetValue(PreviewRowMinWidthProperty, value);
    }

    public MediaBrush PreviewRowBackgroundBrush
    {
        get => (MediaBrush)GetValue(PreviewRowBackgroundBrushProperty);
        set => SetValue(PreviewRowBackgroundBrushProperty, value);
    }

    public MediaBrush PreviewBorderBrush
    {
        get => (MediaBrush)GetValue(PreviewBorderBrushProperty);
        set => SetValue(PreviewBorderBrushProperty, value);
    }

    public MediaBrush PreviewTextBrush
    {
        get => (MediaBrush)GetValue(PreviewTextBrushProperty);
        set => SetValue(PreviewTextBrushProperty, value);
    }

    public MediaBrush PreviewValueBrush
    {
        get => (MediaBrush)GetValue(PreviewValueBrushProperty);
        set => SetValue(PreviewValueBrushProperty, value);
    }

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly BatteryMonitorService _monitor;
    private readonly Action _applyOverlay;
    private bool _allowClose;

    public MainWindow(AppSettings settings, SettingsService settingsService, BatteryMonitorService monitor, Action applyOverlay)
    {
        _settings = settings;
        _settingsService = settingsService;
        _monitor = monitor;
        _applyOverlay = applyOverlay;
        InitializeComponent();
        Loaded += (_, _) => InitializeUi();
        Closing += (s, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            AppController.Instance?.HideSettingsToTray();
        };
    }

    private void InitializeUi()
    {
        DevicesGrid.ItemsSource = _monitor.Devices;
        var initialPreviewDevices = _monitor.Devices.Where(d => d.BatteryPercent.HasValue).ToList();
        PreviewItems.ItemsSource = initialPreviewDevices;
        _monitor.DevicesUpdated += (_, devices) =>
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var previewDevices = devices.Where(d => d.BatteryPercent.HasValue).ToList();
                    PreviewItems.ItemsSource = previewDevices;
                    OverlayTabPreviewItems.ItemsSource = previewDevices;
                });
            }
            catch
            {
                // Preview refresh is non-critical.
            }
        };

        MonitorCombo.Items.Clear();
        var screens = FormsScreen.AllScreens
            .Cast<FormsScreen>()
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < screens.Count; i++)
        {
            var screen = screens[i];
            var label = $"Display {i + 1}";
            if (screen.Primary) label += " · Primary";
            label += $" · {screen.Bounds.Width}×{screen.Bounds.Height}";
            MonitorCombo.Items.Add(new DisplayOption(label, screen.DeviceName));
        }

        MonitorCombo.SelectedItem = MonitorCombo.Items
            .OfType<DisplayOption>()
            .FirstOrDefault(option => option.DeviceName.Equals(_settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? MonitorCombo.Items.OfType<DisplayOption>().FirstOrDefault(option => FormsScreen.AllScreens.Any(s => s.Primary && s.DeviceName == option.DeviceName))
            ?? MonitorCombo.Items.OfType<DisplayOption>().First();

        PositionCombo.ItemsSource = Enum.GetValues(typeof(OverlayPosition));
        PositionCombo.SelectedItem = _settings.Position;

        OffsetXBox.Text = _settings.OffsetX.ToString();
        OffsetYBox.Text = _settings.OffsetY.ToString();
        RefreshSecondsBox.Text = _settings.RefreshSeconds.ToString();
        OpacityBox.Text = _settings.OverlayOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        BgOpacityBox.Text = _settings.OverlayBackgroundOpacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        FontSizeBox.Text = _settings.FontSize.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        LowBatteryBox.Text = _settings.LowBatteryThreshold.ToString();
        OverlayPaddingBox.Text = _settings.OverlayPadding.ToString();
        RowPaddingXBox.Text = _settings.OverlayRowPaddingX.ToString();
        RowPaddingYBox.Text = _settings.OverlayRowPaddingY.ToString();
        RowSpacingBox.Text = _settings.OverlayRowSpacing.ToString();
        MinWidthBox.Text = _settings.OverlayMinWidth.ToString();
        CornerRadiusBox.Text = _settings.OverlayCornerRadius.ToString();
        BgColorBox.Text = _settings.OverlayBackgroundColor;
        RowBgColorBox.Text = _settings.OverlayRowBackgroundColor;
        BorderColorBox.Text = _settings.OverlayBorderColor;
        TextColorBox.Text = _settings.OverlayTextColor;
        ValueColorBox.Text = _settings.OverlayValueColor;

        OverlayTabPreviewItems.ItemsSource = PreviewItems.ItemsSource;

        ClickThroughCheck.IsChecked = _settings.ClickThrough;
        ShowUnknownCheck.IsChecked = _settings.ShowUnknownDevices;
        ShowLaptopCheck.IsChecked = _settings.ShowLaptopBattery;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        RazerDirectCheck.IsChecked = _settings.EnableRazerDirectBatteryReader;
        LogitechG733DirectCheck.IsChecked = _settings.EnableLogitechG733DirectBatteryReader;
        HeadsetControlCheck.IsChecked = _settings.EnableHeadsetControlCliReader;
        ShowOverlayBackgroundCheck.IsChecked = _settings.ShowOverlayBackground;
        ShowOverlayShadowCheck.IsChecked = _settings.ShowOverlayShadow;

        RefreshHintsList();
        UpdatePreviewChrome();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Refreshing...";
        await _monitor.RefreshAsync();
        StatusText.Text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
    }

    private void ToggleOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        AppController.Instance?.ToggleOverlay();
        ToggleOverlayButton.Content = AppController.Instance?.OverlayVisible == true ? "Hide overlay" : "Show overlay";
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySettingsFromUi();
        StatusText.Text = "Settings saved. Overlay updated and realtime monitoring continues in the tray.";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => AppController.Instance?.HideSettingsToTray();

    private void ExitButton_Click(object sender, RoutedEventArgs e) => AppController.Instance?.Exit();

    public void AllowClose() => _allowClose = true;


    public void ShowTrayWarning(string logPath)
    {
        StatusText.Text = "Tray icon could not be created. The app will stay visible/minimized instead of hiding. Log: " + logPath;
    }

    public void ShowOverlayWarning(string logPath)
    {
        StatusText.Text = "Overlay could not be shown. Check the startup log: " + logPath;
    }

    public void ShowMonitorWarning(string logPath)
    {
        StatusText.Text = "Battery monitor could not be started. Check the startup log: " + logPath;
    }

    private void AddHint_Click(object sender, RoutedEventArgs e)
    {
        var text = HintBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_settings.KnownDeviceHints.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
        {
            _settings.KnownDeviceHints.Add(text);
            RefreshHintsList();
            HintBox.Clear();
            ApplySettingsFromUi(refresh: false);
        }
    }

    private void RemoveHint_Click(object sender, RoutedEventArgs e)
    {
        if (HintsList.SelectedItem is not string selected) return;
        _settings.KnownDeviceHints.RemoveAll(x => x.Equals(selected, StringComparison.OrdinalIgnoreCase));
        RefreshHintsList();
        ApplySettingsFromUi(refresh: false);
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(StartupLogger.LogFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = StartupLogger.LogFolder,
            UseShellExecute = true
        });
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        const string url = "https://github.com/sparta1st/UniversalBatteryOverlay/issues/new/choose";
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void ApplySettingsFromUi(bool refresh = true)
    {
        _settings.MonitorDeviceName = MonitorCombo.SelectedItem is DisplayOption displayOption ? displayOption.DeviceName : "Primary";
        if (PositionCombo.SelectedItem is OverlayPosition pos) _settings.Position = pos;
        _settings.OffsetX = ParseInt(OffsetXBox.Text, _settings.OffsetX);
        _settings.OffsetY = ParseInt(OffsetYBox.Text, _settings.OffsetY);
        _settings.RefreshSeconds = ParseInt(RefreshSecondsBox.Text, _settings.RefreshSeconds);
        _settings.OverlayOpacity = ParseDouble(OpacityBox.Text, _settings.OverlayOpacity);
        _settings.OverlayBackgroundOpacity = ParseDouble(BgOpacityBox.Text, _settings.OverlayBackgroundOpacity);
        _settings.FontSize = ParseDouble(FontSizeBox.Text, _settings.FontSize);
        _settings.LowBatteryThreshold = ParseInt(LowBatteryBox.Text, _settings.LowBatteryThreshold);
        _settings.OverlayPadding = ParseInt(OverlayPaddingBox.Text, _settings.OverlayPadding);
        _settings.OverlayRowPaddingX = ParseInt(RowPaddingXBox.Text, _settings.OverlayRowPaddingX);
        _settings.OverlayRowPaddingY = ParseInt(RowPaddingYBox.Text, _settings.OverlayRowPaddingY);
        _settings.OverlayRowSpacing = ParseInt(RowSpacingBox.Text, _settings.OverlayRowSpacing);
        _settings.OverlayMinWidth = ParseInt(MinWidthBox.Text, _settings.OverlayMinWidth);
        _settings.OverlayCornerRadius = ParseInt(CornerRadiusBox.Text, _settings.OverlayCornerRadius);
        _settings.OverlayBackgroundColor = BgColorBox.Text;
        _settings.OverlayRowBackgroundColor = RowBgColorBox.Text;
        _settings.OverlayBorderColor = BorderColorBox.Text;
        _settings.OverlayTextColor = TextColorBox.Text;
        _settings.OverlayValueColor = ValueColorBox.Text;

        _settings.ClickThrough = ClickThroughCheck.IsChecked == true;
        _settings.ShowUnknownDevices = ShowUnknownCheck.IsChecked == true;
        _settings.ShowLaptopBattery = ShowLaptopCheck.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _settings.EnableRazerDirectBatteryReader = RazerDirectCheck.IsChecked == true;
        _settings.EnableLogitechG733DirectBatteryReader = LogitechG733DirectCheck.IsChecked == true;
        _settings.EnableHeadsetControlCliReader = HeadsetControlCheck.IsChecked == true;
        _settings.EnableUniversalHidBatteryReader = false;
        _settings.ShowOverlayBackground = ShowOverlayBackgroundCheck.IsChecked == true;
        _settings.ShowOverlayShadow = ShowOverlayShadowCheck.IsChecked == true;

        _settingsService.Save(_settings);
        _settingsService.SetAutostart(_settings.StartWithWindows);
        _monitor.UpdateInterval();
        _applyOverlay();
        UpdatePreviewChrome();
        if (refresh) _ = _monitor.RefreshAsync();
    }

    private void UpdatePreviewChrome()
    {
        var background = ParseColor(_settings.OverlayBackgroundColor, MediaColor.FromRgb(11, 16, 32));
        var rowBackground = ParseColor(_settings.OverlayRowBackgroundColor, MediaColor.FromRgb(17, 26, 46));
        var border = ParseColor(_settings.OverlayBorderColor, MediaColor.FromRgb(51, 65, 85));
        var text = ParseColor(_settings.OverlayTextColor, MediaColor.FromRgb(248, 250, 252));
        var value = ParseColor(_settings.OverlayValueColor, MediaColor.FromRgb(233, 213, 255));

        PreviewChrome.Padding = new Thickness(_settings.OverlayPadding);
        PreviewChrome.CornerRadius = new CornerRadius(_settings.OverlayCornerRadius);
        PreviewChrome.MinWidth = _settings.OverlayMinWidth + (_settings.OverlayPadding * 2);
        OverlayTabPreviewChrome.Padding = PreviewChrome.Padding;
        OverlayTabPreviewChrome.CornerRadius = PreviewChrome.CornerRadius;
        OverlayTabPreviewChrome.MinWidth = PreviewChrome.MinWidth;
        PreviewItems.FontSize = _settings.FontSize;
        OverlayTabPreviewItems.FontSize = _settings.FontSize;
        PreviewRowPadding = new Thickness(_settings.OverlayRowPaddingX, _settings.OverlayRowPaddingY, _settings.OverlayRowPaddingX, _settings.OverlayRowPaddingY);
        PreviewRowMargin = new Thickness(0, _settings.OverlayRowSpacing, 0, _settings.OverlayRowSpacing);
        PreviewRowMinWidth = _settings.OverlayMinWidth;
        PreviewBorderBrush = new SolidColorBrush(border);
        PreviewTextBrush = new SolidColorBrush(text);
        PreviewValueBrush = new SolidColorBrush(value);
        PreviewRowBackgroundBrush = new SolidColorBrush(WithAlpha(rowBackground, Math.Min(0.95, _settings.OverlayBackgroundOpacity + 0.12)));

        var chromeBrush = _settings.ShowOverlayBackground
            ? new SolidColorBrush(WithAlpha(background, _settings.OverlayBackgroundOpacity))
            : MediaBrushes.Transparent;
        var chromeBorder = _settings.ShowOverlayBackground ? new Thickness(1) : new Thickness(0);
        var shadow = _settings.ShowOverlayShadow
            ? new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.28 }
            : null;

        PreviewChrome.BorderBrush = PreviewBorderBrush;
        PreviewChrome.BorderThickness = chromeBorder;
        PreviewChrome.Background = chromeBrush;
        PreviewChrome.Effect = shadow;
        OverlayTabPreviewChrome.BorderBrush = PreviewBorderBrush;
        OverlayTabPreviewChrome.BorderThickness = chromeBorder;
        OverlayTabPreviewChrome.Background = chromeBrush;
        OverlayTabPreviewChrome.Effect = _settings.ShowOverlayShadow
            ? new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.28 }
            : null;
    }

    private void RefreshHintsList()
    {
        HintsList.ItemsSource = null;
        HintsList.ItemsSource = _settings.KnownDeviceHints.OrderBy(x => x).ToList();
    }

    private sealed record DisplayOption(string Label, string DeviceName)
    {
        public override string ToString() => Label;
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

    private static int ParseInt(string text, int fallback) => int.TryParse(text, out var value) ? value : fallback;
    private static double ParseDouble(string text, double fallback) => double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
