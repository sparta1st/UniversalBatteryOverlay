using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

public interface IBatteryReader
{
    string Name { get; }
    Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken);
}
