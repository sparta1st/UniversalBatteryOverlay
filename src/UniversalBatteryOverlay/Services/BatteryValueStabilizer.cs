using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Services;

/// <summary>
/// Keeps realtime polling useful without showing fake spikes from some dongle headsets while charging.
/// Logitech G733 can report unstable values while plugged in, for example 55/57/100, then drop back
/// to the real level immediately when unplugged. This stabilizer keeps the last trusted percentage,
/// shows the charging glyph, and only accepts charging changes when they are plausible and stable.
/// </summary>
public sealed class BatteryValueStabilizer
{
    private sealed class State
    {
        public int? LastTrustedPercent { get; set; }
        public DateTime LastTrustedAt { get; set; }
        public bool? LastCharging { get; set; }
        public int? CandidatePercent { get; set; }
        public int CandidateCount { get; set; }
        public DateTime CandidateStartedAt { get; set; }
    }

    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    public DeviceBatteryInfo Apply(string key, DeviceBatteryInfo device)
    {
        var now = DateTime.Now;
        var state = GetState(key);
        var isHeadset = IsHeadset(key, device);
        var isCharging = device.IsCharging == true;

        if (device.BatteryPercent.HasValue)
            device.BatteryPercent = Math.Clamp(device.BatteryPercent.Value, 0, 100);

        // For devices that are not known to lie while charging, keep the raw reader value.
        if (!isHeadset)
        {
            if (device.BatteryPercent.HasValue)
                Trust(state, device.BatteryPercent.Value, device.IsCharging, now);
            return device;
        }

        // When unplugged/discharging, the G733 value returns to the normal real value. Trust it immediately.
        if (!isCharging)
        {
            if (device.BatteryPercent.HasValue)
                Trust(state, device.BatteryPercent.Value, false, now);
            return device;
        }

        // Charging but no level from the reader: show the lightning mark with the last real level if we have one.
        if (!device.BatteryPercent.HasValue)
        {
            if (state.LastTrustedPercent.HasValue)
            {
                device.BatteryPercent = state.LastTrustedPercent.Value;
                AppendStatus(device, "Charging; showing last trusted percentage");
            }

            state.LastCharging = true;
            return device;
        }

        var incoming = device.BatteryPercent.Value;

        // If the headset suddenly says 100% while the last real value was nowhere near full, it is a charging spike.
        if (incoming == 100 && state.LastTrustedPercent is > 0 and < 95)
        {
            HoldLastTrusted(device, state, incoming, "Ignored fake 100% charging spike");
            return device;
        }

        if (!state.LastTrustedPercent.HasValue)
        {
            // First value after app start. Accept anything except the common fake 100% spike.
            if (incoming == 100)
            {
                device.BatteryPercent = null;
                AppendStatus(device, "Charging detected; waiting for a stable real percentage");
                state.LastCharging = true;
                return device;
            }

            Trust(state, incoming, true, now);
            AppendStatus(device, "Charging percentage accepted as initial trusted value");
            return device;
        }

        var last = state.LastTrustedPercent.Value;
        var delta = incoming - last;
        var timeSinceTrust = now - state.LastTrustedAt;

        // Real charging should not jump instantly by large amounts. Hold the last trusted value instead.
        if (Math.Abs(delta) >= 4 && timeSinceTrust < TimeSpan.FromMinutes(8))
        {
            HoldLastTrusted(device, state, incoming, $"Ignored unstable charging jump from {last}% to {incoming}%");
            RegisterCandidate(state, incoming, now);
            return device;
        }

        // Small changes are accepted only after the same value stays stable for several realtime polls.
        if (delta != 0)
        {
            RegisterCandidate(state, incoming, now);
            var stableFor = now - state.CandidateStartedAt;
            if (state.CandidateCount < 4 || stableFor < TimeSpan.FromSeconds(6))
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
        state = new State { LastTrustedAt = DateTime.MinValue };
        _states[key] = state;
        return state;
    }

    private static bool IsHeadset(string key, DeviceBatteryInfo device)
    {
        return key.Contains("g733", StringComparison.OrdinalIgnoreCase)
            || device.DeviceType.Equals("Headset", StringComparison.OrdinalIgnoreCase)
            || device.DeviceType.Equals("Headphones", StringComparison.OrdinalIgnoreCase)
            || device.Name.Contains("G733", StringComparison.OrdinalIgnoreCase)
            || device.Name.Contains("headset", StringComparison.OrdinalIgnoreCase);
    }

    private static void Trust(State state, int percent, bool? charging, DateTime now)
    {
        state.LastTrustedPercent = Math.Clamp(percent, 0, 100);
        state.LastTrustedAt = now;
        state.LastCharging = charging;
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
