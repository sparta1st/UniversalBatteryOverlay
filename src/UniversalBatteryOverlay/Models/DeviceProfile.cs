namespace UniversalBatteryOverlay.Models;

public sealed class DeviceProfile
{
    public string DisplayName { get; set; } = "Wireless device";
    public string DeviceType { get; set; } = "Device";
    public List<string> HardwareIds { get; set; } = new();
    public List<string> Aliases { get; set; } = new();
    public bool PassiveOnly { get; set; } = true;
    public bool ShowWhenDetectedWithoutBattery { get; set; } = true;
    public string Notes { get; set; } = string.Empty;

    public IEnumerable<string> MatchTokens => HardwareIds.Concat(Aliases).Where(x => !string.IsNullOrWhiteSpace(x));
}
