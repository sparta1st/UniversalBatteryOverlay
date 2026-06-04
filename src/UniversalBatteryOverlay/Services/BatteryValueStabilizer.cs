using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Services;

/// <summary>
/// Smooths realtime battery values without sending any device commands.
///
/// Some wireless devices, especially dongle headsets, report temporary/fake percentages while charging
/// (for example 55 -> 57 -> 100 -> 55). This class never invents a new level. Instead, it keeps the last
/// trusted unplugged/stable value and only accepts charging changes after they remain stable and plausible.
/// </summary>
public sealed class BatteryValueStabilizer
{
    private static readonly TimeSpan ChargingFreezeWindow = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ChargingCandidateStableTime = TimeSpan.FromSeconds(20);
    private const int ChargingCandidateRequiredSamples = 8;
    private sealed class State
    {
        public int? LastTrustedPercent { get; set; }
        public DateTime LastTrustedAt { get; set; }
        public bool? LastCharging { get; set; }
        public int? CandidatePercent { get; set; }
        public int CandidateCount { get; set; }
        public DateTime CandidateStartedAt { get; set; }
        public DateTime FirstChargingSeenAt { get; set; }
        public int? LastRawChargingPercent { get; set; }
    }

    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    public DeviceBatteryInfo Apply(string key, DeviceBatteryInfo device)
    {
        var now = DateTime.Now;
        var state = GetState(key);

        if (device.BatteryPercent.HasValue)
            device.BatteryPercent = Math.Clamp(device.BatteryPercent.Value, 0, 100);

        // System/laptop batteries are reported by Windows and should not be smoothed.
        if (IsSystemBattery(device))
        {
            if (device.BatteryPercent.HasValue)
                Trust(state, device.BatteryPercent.Value, device.IsCharging, now);
            return device;
        }

        var shouldStabilize = ShouldStabilizeWirelessDevice(key, device);
        var isCharging = device.IsCharging == true;

        if (!shouldStabilize)
        {
            if (device.BatteryPercent.HasValue)
                Trust(state, device.BatteryPercent.Value, device.IsCharging, now);
            return device;
        }

        // Unplugged values are usually the real values. Trust them immediately.
        if (!isCharging)
        {
            if (device.BatteryPercent.HasValue)
                Trust(state, device.BatteryPercent.Value, false, now);
            else
                state.LastCharging = false;
            return device;
        }

        // Charging started. Keep the last trusted value for a short grace period.
        // This removes the instant fake spike many headsets report when USB charging begins.
        if (state.LastCharging != true)
        {
            state.FirstChargingSeenAt = now;
            state.CandidatePercent = null;
            state.CandidateCount = 0;
            state.CandidateStartedAt = DateTime.MinValue;
        }
        state.LastCharging = true;

        if (!device.BatteryPercent.HasValue)
        {
            if (state.LastTrustedPercent.HasValue)
            {
                device.BatteryPercent = state.LastTrustedPercent.Value;
                AppendStatus(device, "Charging; showing last trusted percentage");
            }
            return device;
        }

        var incoming = device.BatteryPercent.Value;
        state.LastRawChargingPercent = incoming;

        // If this is the first value ever seen and it arrives while already charging, we can only accept it
        // if it is not the classic fake 100% value. Otherwise show charging without a trusted percentage.
        if (!state.LastTrustedPercent.HasValue)
        {
            if (incoming >= 98)
            {
                device.BatteryPercent = null;
                AppendStatus(device, "Charging detected; waiting for a stable real percentage");
                return device;
            }

            RegisterCandidate(state, incoming, now);
            var stableForInitial = now - state.CandidateStartedAt;
            if (state.CandidateCount < ChargingCandidateRequiredSamples || stableForInitial < ChargingCandidateStableTime)
            {
                device.BatteryPercent = null;
                AppendStatus(device, $"Charging; waiting for initial stable percentage ({incoming}%)");
                return device;
            }

            Trust(state, incoming, true, now);
            AppendStatus(device, "Charging percentage accepted after stabilization");
            return device;
        }

        var last = state.LastTrustedPercent.Value;
        var delta = incoming - last;
        var timeSinceTrust = now - state.LastTrustedAt;
        var chargingFor = now - state.FirstChargingSeenAt;

        // Ignore instant fake 100% when the last real value was clearly lower.
        if (incoming >= 98 && last < 95)
        {
            HoldLastTrusted(device, state, incoming, "Ignored fake charging spike near 100%");
            return device;
        }

        // During the first seconds after plugging in, freeze the last trusted value.
        if (chargingFor < ChargingFreezeWindow && incoming != last)
        {
            HoldLastTrusted(device, state, incoming, $"Charging just started; raw {incoming}% held until stable");
            RegisterCandidate(state, incoming, now);
            return device;
        }

        // Battery cannot realistically drop while charging. Keep the trusted value unless it stays stable
        // for a long time, because some devices bounce down/up while plugged in.
        if (delta < 0)
        {
            HoldLastTrusted(device, state, incoming, $"Ignored charging drop from {last}% to {incoming}%");
            RegisterCandidate(state, incoming, now);
            return device;
        }

        // Real charging is slow. Do not accept large instant jumps.
        if (delta >= 2 && timeSinceTrust < TimeSpan.FromMinutes(10))
        {
            HoldLastTrusted(device, state, incoming, $"Ignored unstable charging jump from {last}% to {incoming}%");
            RegisterCandidate(state, incoming, now);
            return device;
        }

        // Even +1/+2 changes must be stable for a few realtime polls before showing them.
        if (delta != 0)
        {
            RegisterCandidate(state, incoming, now);
            var stableFor = now - state.CandidateStartedAt;
            if (state.CandidateCount < ChargingCandidateRequiredSamples || stableFor < ChargingCandidateStableTime)
            {
                HoldLastTrusted(device, state, incoming, $"Waiting for charging value {incoming}% to stabilize");
                return device;
            }
        }

        Trust(state, incoming, true, now);
        if (delta != 0)
            AppendStatus(device, "Charging percentage stabilized and accepted");
        return device;
    }

    private State GetState(string key)
    {
        if (_states.TryGetValue(key, out var state)) return state;
        state = new State { LastTrustedAt = DateTime.MinValue, FirstChargingSeenAt = DateTime.MinValue };
        _states[key] = state;
        return state;
    }

    private static bool IsSystemBattery(DeviceBatteryInfo device)
        => device.DeviceType.Equals("Laptop", StringComparison.OrdinalIgnoreCase)
        || device.DeviceType.Equals("Battery", StringComparison.OrdinalIgnoreCase)
        || device.Name.Contains("laptop", StringComparison.OrdinalIgnoreCase)
        || device.Name.Contains("system", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldStabilizeWirelessDevice(string key, DeviceBatteryInfo device)
    {
        var text = $"{key} {device.Name} {device.DeviceType} {device.Reader} {device.RawId}";
        if (text.Contains("g733", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Contains("viper", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Contains("wireless", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Contains("lightspeed", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Contains("dongle", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Contains("receiver", StringComparison.OrdinalIgnoreCase)) return true;

        return device.DeviceType.Equals("Headset", StringComparison.OrdinalIgnoreCase)
            || device.DeviceType.Equals("Headphones", StringComparison.OrdinalIgnoreCase)
            || device.DeviceType.Equals("Mouse", StringComparison.OrdinalIgnoreCase)
            || device.DeviceType.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)
            || device.DeviceType.Equals("Controller", StringComparison.OrdinalIgnoreCase);
    }

    private static void Trust(State state, int percent, bool? charging, DateTime now)
    {
        state.LastTrustedPercent = Math.Clamp(percent, 0, 100);
        state.LastTrustedAt = now;
        state.LastCharging = charging;
        if (charging != true)
            state.FirstChargingSeenAt = DateTime.MinValue;
        state.CandidatePercent = null;
        state.CandidateCount = 0;
        state.CandidateStartedAt = DateTime.MinValue;
    }

    private static void RegisterCandidate(State state, int percent, DateTime now)
    {
        if (state.CandidatePercent == percent)
        {
            state.CandidateCount++;
            return;
        }

        state.CandidatePercent = percent;
        state.CandidateCount = 1;
        state.CandidateStartedAt = now;
    }

    private static void HoldLastTrusted(DeviceBatteryInfo device, State state, int rawPercent, string reason)
    {
        if (state.LastTrustedPercent.HasValue)
            device.BatteryPercent = state.LastTrustedPercent.Value;
        else
            device.BatteryPercent = null;

        AppendStatus(device, $"{reason}; raw {rawPercent}% held back");
    }

    private static void AppendStatus(DeviceBatteryInfo device, string text)
    {
        if (string.IsNullOrWhiteSpace(device.Status))
            device.Status = text;
        else if (!device.Status.Contains(text, StringComparison.OrdinalIgnoreCase))
            device.Status += " · " + text;
    }
}
