using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using FormsScreen = System.Windows.Forms.Screen;
using UniversalBatteryOverlay.Models;
using UniversalBatteryOverlay.Services;

namespace UniversalBatteryOverlay;

public partial class MainWindow : Window
{
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
        PreviewItems.ItemsSource = _monitor.Devices;

        MonitorCombo.Items.Clear();
        MonitorCombo.Items.Add("Primary");
        foreach (var screen in FormsScreen.AllScreens)
        {
            MonitorCombo.Items.Add(screen.DeviceName);
        }
        MonitorCombo.SelectedItem = MonitorCombo.Items.Contains(_settings.MonitorDeviceName)
            ? _settings.MonitorDeviceName
            : "Primary";

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

        ClickThroughCheck.IsChecked = _settings.ClickThrough;
        ShowUnknownCheck.IsChecked = _settings.ShowUnknownDevices;
        ShowLaptopCheck.IsChecked = _settings.ShowLaptopBattery;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        EnableRazerDirectCheck.IsChecked = _settings.EnableRazerDirectBatteryReader;
        EnableUniversalHidCheck.IsChecked = _settings.EnableUniversalHidBatteryReader;
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

    private void OpenReadersFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = PathsService.ReadersFolder;
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void ApplySettingsFromUi(bool refresh = true)
    {
        _settings.MonitorDeviceName = MonitorCombo.SelectedItem?.ToString() ?? "Primary";
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

        _settings.ClickThrough = ClickThroughCheck.IsChecked == true;
        _settings.ShowUnknownDevices = ShowUnknownCheck.IsChecked == true;
        _settings.ShowLaptopBattery = ShowLaptopCheck.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _settings.EnableRazerDirectBatteryReader = EnableRazerDirectCheck.IsChecked == true;
        _settings.EnableUniversalHidBatteryReader = EnableUniversalHidCheck.IsChecked == true;
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
        PreviewChrome.Padding = new Thickness(_settings.OverlayPadding);
        PreviewChrome.CornerRadius = new CornerRadius(_settings.OverlayCornerRadius);
        PreviewChrome.BorderThickness = _settings.ShowOverlayBackground ? new Thickness(1) : new Thickness(0);
        PreviewChrome.Background = _settings.ShowOverlayBackground
            ? new SolidColorBrush(MediaColor.FromArgb(ToByte(_settings.OverlayBackgroundOpacity), 11, 16, 32))
            : MediaBrushes.Transparent;
        PreviewChrome.Effect = _settings.ShowOverlayShadow
            ? new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.28 }
            : null;
    }

    private void RefreshHintsList()
    {
        HintsList.ItemsSource = null;
        HintsList.ItemsSource = _settings.KnownDeviceHints.OrderBy(x => x).ToList();
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
    private static int ParseInt(string text, int fallback) => int.TryParse(text, out var value) ? value : fallback;
    private static double ParseDouble(string text, double fallback) => double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
