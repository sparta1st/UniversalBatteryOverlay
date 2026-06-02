using System.Windows.Forms;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

public sealed class SystemBatteryReader : IBatteryReader
{
    public string Name => "Windows system battery";

    public Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        var status = SystemInformation.PowerStatus;
        var percent = status.BatteryLifePercent >= 0
            ? (int)Math.Round(status.BatteryLifePercent * 100)
            : (int?)null;

        var hasBattery = status.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery;
        if (!hasBattery)
        {
            return Task.FromResult<IReadOnlyList<DeviceBatteryInfo>>(Array.Empty<DeviceBatteryInfo>());
        }

        var info = new DeviceBatteryInfo
        {
            Name = "Laptop battery",
            DeviceType = "Laptop",
            BatteryPercent = percent,
            IsCharging = status.PowerLineStatus == PowerLineStatus.Online,
            Status = status.PowerLineStatus == PowerLineStatus.Online ? "AC power / charging" : "On battery",
            Reader = Name,
            RawId = "SYSTEM",
            LastUpdated = DateTime.Now
        };

        return Task.FromResult<IReadOnlyList<DeviceBatteryInfo>>(new[] { info });
    }
}
