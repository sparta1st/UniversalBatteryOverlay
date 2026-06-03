namespace UniversalBatteryOverlay.Models;

public sealed class DeviceBatteryInfo
{
    public string Name { get; set; } = "Unknown device";
    public string DeviceType { get; set; } = "Device";
    public int? BatteryPercent { get; set; }
    public bool? IsCharging { get; set; }
    public string Status { get; set; } = "Unknown";
    public string Reader { get; set; } = "Unknown";
    public string? RawId { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public string BatteryDisplay => BatteryPercent.HasValue ? $"{BatteryPercent.Value}%" : "—";
    public string ChargeGlyph => IsCharging == true ? "⚡" : string.Empty;
    public string BatteryWithChargingDisplay => IsCharging == true
        ? $"⚡ {BatteryDisplay}"
        : BatteryDisplay;

    public string ChargingDisplay => IsCharging == true ? "Charging" : IsCharging == false ? "Not charging" : "Unknown";

    public string CleanStatusDisplay
    {
        get
        {
            var stable = Status.Contains("stabil", StringComparison.OrdinalIgnoreCase)
                      || Status.Contains("held back", StringComparison.OrdinalIgnoreCase)
                      || Status.Contains("trusted", StringComparison.OrdinalIgnoreCase);

            if (IsCharging == true && BatteryPercent.HasValue && stable) return "Charging · stabilized percentage";
            if (IsCharging == true && BatteryPercent.HasValue) return "Charging · live percentage";
            if (IsCharging == true) return "Charging · waiting for percentage";
            if (BatteryPercent.HasValue) return "Live percentage";

            var lower = ($"{Name} {DeviceType} {Status} {Reader}").ToLowerInvariant();
            if (lower.Contains("g733") || lower.Contains("logitech"))
            {
                if (lower.Contains("direct") || lower.Contains("headsetcontrol") || lower.Contains("failed"))
                    return "Detected · headset battery reader failed";
                return "Detected · waiting for headset reader";
            }

            if (lower.Contains("qwertykey") || lower.Contains("keyboard"))
                return "Detected safely · passive keyboard mode";

            return "Detected · percentage unavailable";
        }
    }

    public string MainWindowDetailsDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Status)) return Reader;
            var shortStatus = Status;
            if (shortStatus.Length > 96) shortStatus = shortStatus[..96] + "...";
            return shortStatus;
        }
    }

    public string TypeDisplay => DeviceType.ToLowerInvariant() switch
    {
        "mouse" => "Mouse",
        "keyboard" => "Keyboard",
        "headset" => "Headset",
        "headphones" => "Headset",
        "laptop" => "Laptop",
        "battery" => "Battery",
        "controller" => "Controller",
        _ => "Device"
    };

    public string LastUpdatedDisplay => LastUpdated == default ? string.Empty : LastUpdated.ToString("HH:mm:ss");

    public string OverlayDisplay => $"{TypeDisplay}  {BatteryWithChargingDisplay}";

    public string Icon => DeviceType.ToLowerInvariant() switch
    {
        "mouse" => "🖱",
        "keyboard" => "⌨",
        "headset" => "🎧",
        "headphones" => "🎧",
        "laptop" => "💻",
        "battery" => "🔋",
        "controller" => "🎮",
        _ => "🔌"
    };
}
