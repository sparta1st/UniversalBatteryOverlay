using System.Text.Json;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Services;

public sealed class DeviceProfileCatalog
{
    private readonly List<DeviceProfile> _profiles;

    public DeviceProfileCatalog()
    {
        _profiles = BuildDefaultProfiles();
        LoadExternalProfiles(_profiles);
    }

    public IReadOnlyList<DeviceProfile> Profiles => _profiles;

    public IReadOnlyList<string> PassiveBatteryHints => _profiles
        .SelectMany(p => p.MatchTokens)
        .Concat(new[]
        {
            "Wireless", "Bluetooth", "BLE", "Receiver", "Dongle", "Lightspeed",
            "Headset", "Headphone", "Mouse", "Keyboard", "Controller", "Gamepad"
        })
        .Where(IsSafeHint)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public DeviceProfile? FindBestProfile(string? name, string? rawId, string? deviceType = null)
    {
        var text = $"{name} {rawId} {deviceType}";
        return _profiles
            .Select(p => new { Profile = p, Score = ScoreProfile(p, text) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Profile)
            .FirstOrDefault();
    }

    public static IReadOnlyList<DeviceProfile> DefaultProfiles => BuildDefaultProfiles();

    private static int ScoreProfile(DeviceProfile profile, string text)
    {
        var score = 0;
        foreach (var id in profile.HardwareIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && text.Contains(id, StringComparison.OrdinalIgnoreCase))
                score += 1000 + id.Length;
        }

        foreach (var alias in profile.Aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias) && text.Contains(alias, StringComparison.OrdinalIgnoreCase))
                score += 100 + alias.Length;
        }

        return score;
    }

    private static bool IsSafeHint(string hint)
    {
        if (string.IsNullOrWhiteSpace(hint) || hint.Length < 3) return false;
        var lower = hint.Trim().ToLowerInvariant();

        // Avoid vendor-only strings because those would make unrelated USB/RGB/audio devices appear.
        var unsafeVendorOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "logitech", "razer", "corsair", "steelseries", "hyperx", "asus", "sony", "microsoft",
            "keyboard", "mouse", "headset", "controller", "wireless", "bluetooth", "receiver", "dongle"
        };

        return !unsafeVendorOnly.Contains(lower);
    }

    private static void LoadExternalProfiles(List<DeviceProfile> profiles)
    {
        foreach (var path in CandidateProfileFiles())
        {
            try
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var external = JsonSerializer.Deserialize<List<DeviceProfile>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (external is null) continue;
                foreach (var profile in external.Where(IsValidExternalProfile))
                {
                    var key = ProfileKey(profile);
                    var existingIndex = profiles.FindIndex(p => ProfileKey(p).Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (existingIndex >= 0) profiles[existingIndex] = profile;
                    else profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Error($"Failed to load device profile file: {path}", ex);
            }
        }
    }

    private static IEnumerable<string> CandidateProfileFiles()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "device-profiles.json");
        yield return Path.Combine(PathsService.AppDataFolder, "device-profiles.json");
    }

    private static bool IsValidExternalProfile(DeviceProfile p)
        => !string.IsNullOrWhiteSpace(p.DisplayName)
           && !string.IsNullOrWhiteSpace(p.DeviceType)
           && p.MatchTokens.Any(IsSafeHint);

    private static string ProfileKey(DeviceProfile p)
    {
        var firstId = p.HardwareIds.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(firstId)) return firstId.Trim().ToLowerInvariant();
        return p.DisplayName.Trim().ToLowerInvariant();
    }

    private static List<DeviceProfile> BuildDefaultProfiles() => new()
    {
        // Targeted readers included in this app. These are exact-model readers only.
        Profile("Razer Viper V2 Pro", "Mouse", new[] { "VID_1532&PID_00A6", "VID_1532&PID_00A5" }, new[] { "Razer Viper V2 Pro", "Viper V2 Pro" }, passiveOnly: false, "Targeted zero-access Razer reader available."),
        Profile("Logitech G733", "Headset", new[] { "VID_046D&PID_0AB5" }, new[] { "Logitech G733", "G733", "G733 Gaming Headset" }, passiveOnly: false, "Targeted G733 reader and optional headsetcontrol helper available."),
        Profile("QwertyKey Keyboard", "Keyboard", new[] { "VID_36B0&PID_3002" }, new[] { "QwertyKey", "QWERTYKEY", "Qwerty Key" }, passiveOnly: true, "Keyboard stays passive-only to avoid input interference."),

        // Logitech mice.
        Profile("Logitech G Pro Wireless", "Mouse", Array.Empty<string>(), new[] { "G Pro Wireless", "PRO Wireless Gaming Mouse" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G Pro X Superlight", "Mouse", Array.Empty<string>(), new[] { "PRO X SUPERLIGHT", "G Pro X Superlight", "GPRO X Superlight" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G Pro X Superlight 2", "Mouse", Array.Empty<string>(), new[] { "PRO X SUPERLIGHT 2", "G Pro X Superlight 2" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G305 / G304", "Mouse", Array.Empty<string>(), new[] { "G305", "G304", "LIGHTSPEED G305", "LIGHTSPEED G304" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G309 Lightspeed", "Mouse", Array.Empty<string>(), new[] { "G309", "G309 LIGHTSPEED" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G502 Lightspeed", "Mouse", Array.Empty<string>(), new[] { "G502 LIGHTSPEED", "G502 Wireless" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G502 X Lightspeed", "Mouse", Array.Empty<string>(), new[] { "G502 X", "G502 X LIGHTSPEED", "G502 X PLUS" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G703 / G903", "Mouse", Array.Empty<string>(), new[] { "G703", "G903", "G703 LIGHTSPEED", "G903 LIGHTSPEED" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G705", "Mouse", Array.Empty<string>(), new[] { "G705", "Aurora G705" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G604 Lightspeed", "Mouse", Array.Empty<string>(), new[] { "G604", "G604 LIGHTSPEED" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech MX Master", "Mouse", Array.Empty<string>(), new[] { "MX Master", "MX Master 2S", "MX Master 3", "MX Master 3S" }, true, "Bluetooth/Windows battery support when exposed."),
        Profile("Logitech MX Anywhere", "Mouse", Array.Empty<string>(), new[] { "MX Anywhere", "MX Anywhere 2S", "MX Anywhere 3", "MX Anywhere 3S" }, true, "Bluetooth/Windows battery support when exposed."),
        Profile("Logitech Lift / Vertical / Ergo", "Mouse", Array.Empty<string>(), new[] { "Logi Lift", "Lift Vertical", "MX Vertical", "MX Ergo", "ERGO M575" }, true, "Bluetooth/Windows battery support when exposed."),
        Profile("Logitech Pebble / POP Mouse", "Mouse", Array.Empty<string>(), new[] { "Pebble Mouse", "POP Mouse", "M350", "M650" }, true, "Bluetooth/Windows battery support when exposed."),

        // Logitech keyboards and headsets.
        Profile("Logitech MX Keys", "Keyboard", Array.Empty<string>(), new[] { "MX Keys", "MX Keys Mini", "MX Mechanical", "MX Mechanical Mini" }, true, "Keyboard stays passive-only; battery appears if Windows exposes it."),
        Profile("Logitech G915 / G915 TKL", "Keyboard", Array.Empty<string>(), new[] { "G915", "G915 TKL", "G915 LIGHTSPEED" }, true, "Keyboard stays passive-only."),
        Profile("Logitech G515 Lightspeed", "Keyboard", Array.Empty<string>(), new[] { "G515", "G515 LIGHTSPEED" }, true, "Keyboard stays passive-only."),
        Profile("Logitech G613", "Keyboard", Array.Empty<string>(), new[] { "G613", "G613 Wireless" }, true, "Keyboard stays passive-only."),
        Profile("Logitech K380 / K580 / POP Keys", "Keyboard", Array.Empty<string>(), new[] { "K380", "K580", "POP Keys", "ERGO K860" }, true, "Bluetooth/Windows battery support when exposed."),
        Profile("Logitech G435", "Headset", Array.Empty<string>(), new[] { "G435", "G435 Gaming Headset" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G535 / G735", "Headset", Array.Empty<string>(), new[] { "G535", "G735", "Aurora G735" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech G Pro X Wireless Headset", "Headset", Array.Empty<string>(), new[] { "PRO X Wireless", "G Pro X Wireless", "G PRO X Wireless Headset", "PRO X 2 LIGHTSPEED" }, true, "Passive Windows battery support when exposed."),
        Profile("Logitech Zone / H-series", "Headset", Array.Empty<string>(), new[] { "Zone Wireless", "Zone Vibe", "Logitech H800", "Logitech H820e" }, true, "Passive Windows battery support when exposed."),

        // Razer mice, keyboards and headsets. Only Viper V2 Pro has an active reader here.
        Profile("Razer Viper Ultimate / V3 Pro", "Mouse", Array.Empty<string>(), new[] { "Viper Ultimate", "Viper V3 Pro", "Viper V3 HyperSpeed" }, true, "Passive-only until a dedicated reader is added."),
        Profile("Razer DeathAdder Wireless", "Mouse", Array.Empty<string>(), new[] { "DeathAdder V2 Pro", "DeathAdder V3 Pro", "DeathAdder V3 HyperSpeed" }, true, "Passive-only until a dedicated reader is added."),
        Profile("Razer Basilisk Wireless", "Mouse", Array.Empty<string>(), new[] { "Basilisk Ultimate", "Basilisk V3 Pro", "Basilisk X HyperSpeed" }, true, "Passive-only until a dedicated reader is added."),
        Profile("Razer Cobra / Naga / Orochi Wireless", "Mouse", Array.Empty<string>(), new[] { "Cobra Pro", "Naga Pro", "Naga V2 Pro", "Orochi V2", "Pro Click Mini" }, true, "Passive-only until a dedicated reader is added."),
        Profile("Razer BlackWidow / DeathStalker Wireless", "Keyboard", Array.Empty<string>(), new[] { "BlackWidow V3 Pro", "BlackWidow V4 Pro", "DeathStalker V2 Pro" }, true, "Keyboard stays passive-only."),
        Profile("Razer BlackShark / Barracuda / Kraken Wireless", "Headset", Array.Empty<string>(), new[] { "BlackShark V2 Pro", "Barracuda X", "Barracuda Pro", "Kraken V3 Pro", "Kaira Pro" }, true, "Passive Windows battery support when exposed."),

        // SteelSeries.
        Profile("SteelSeries Arctis / Nova Headset", "Headset", Array.Empty<string>(), new[] { "Arctis", "Arctis Nova", "Nova 5", "Nova 7", "Nova Pro Wireless", "Arctis 7", "Arctis 9", "Arctis 1 Wireless" }, true, "Passive Windows battery support when exposed."),
        Profile("SteelSeries Aerox Wireless", "Mouse", Array.Empty<string>(), new[] { "Aerox 3 Wireless", "Aerox 5 Wireless", "Aerox 9 Wireless", "SteelSeries Aerox" }, true, "Passive Windows battery support when exposed."),
        Profile("SteelSeries Rival / Prime Wireless", "Mouse", Array.Empty<string>(), new[] { "Rival 3 Wireless", "Prime Wireless", "Prime Mini Wireless", "SteelSeries Prime" }, true, "Passive Windows battery support when exposed."),
        Profile("SteelSeries Apex Wireless", "Keyboard", Array.Empty<string>(), new[] { "Apex Pro TKL Wireless", "Apex Pro Mini Wireless" }, true, "Keyboard stays passive-only."),

        // Corsair / Elgato ecosystem.
        Profile("Corsair Wireless Headset", "Headset", Array.Empty<string>(), new[] { "Virtuoso", "HS80", "HS70", "HS65 Wireless", "VOID Wireless", "Corsair Headset" }, true, "Passive Windows battery support when exposed."),
        Profile("Corsair Wireless Mouse", "Mouse", Array.Empty<string>(), new[] { "Dark Core", "Harpoon Wireless", "Katar Elite Wireless", "M75 Air", "Sabre RGB Pro Wireless", "Ironclaw RGB Wireless" }, true, "Passive Windows battery support when exposed."),
        Profile("Corsair Wireless Keyboard", "Keyboard", Array.Empty<string>(), new[] { "K57 RGB Wireless", "K63 Wireless", "K70 Pro Mini Wireless", "K65 Plus Wireless" }, true, "Keyboard stays passive-only."),

        // HyperX.
        Profile("HyperX Wireless Headset", "Headset", Array.Empty<string>(), new[] { "Cloud Flight", "Cloud Alpha Wireless", "Cloud II Wireless", "Cloud III Wireless", "Cloud MIX Buds", "HyperX Wireless" }, true, "Passive Windows battery support when exposed."),
        Profile("HyperX Wireless Mouse", "Mouse", Array.Empty<string>(), new[] { "Pulsefire Haste Wireless", "Pulsefire Haste 2 Wireless", "Pulsefire Dart" }, true, "Passive Windows battery support when exposed."),

        // ASUS ROG / TUF.
        Profile("ASUS ROG Wireless Mouse", "Mouse", Array.Empty<string>(), new[] { "ROG Keris", "ROG Harpe", "ROG Chakram", "ROG Gladius", "ROG Spatha", "ROG Pugio" }, true, "Passive Windows battery support when exposed."),
        Profile("ASUS ROG Wireless Keyboard", "Keyboard", Array.Empty<string>(), new[] { "ROG Falchion", "ROG Azoth", "ROG Strix Scope RX TKL Wireless" }, true, "Keyboard stays passive-only."),
        Profile("ASUS ROG Wireless Headset", "Headset", Array.Empty<string>(), new[] { "ROG Delta S Wireless", "ROG Cetra", "ROG Fusion", "TUF H3 Wireless" }, true, "Passive Windows battery support when exposed."),

        // Other popular gaming mice/keyboards/headsets.
        Profile("ROCCAT / Turtle Beach Wireless", "Device", Array.Empty<string>(), new[] { "ROCCAT Kone Pro Air", "Burst Pro Air", "Turtle Beach Stealth", "Stealth 600", "Stealth 700", "Stealth Pro", "Atlas Air" }, true, "Passive Windows battery support when exposed."),
        Profile("Glorious Wireless", "Device", Array.Empty<string>(), new[] { "Glorious Model O Wireless", "Model D Wireless", "Model I Wireless", "GMMK Numpad", "GMMK Pro Wireless" }, true, "Passive Windows battery support when exposed."),
        Profile("Pulsar / Lamzu / Finalmouse / WLmouse", "Mouse", Array.Empty<string>(), new[] { "Pulsar X2", "Pulsar Xlite", "LAMZU Atlantis", "LAMZU Thorn", "LAMZU Maya", "Finalmouse", "WLmouse", "Beast X", "Ninjutso Sora", "Endgame Gear XM2w", "ZOWIE U2" }, true, "Passive Windows battery support when exposed."),
        Profile("Keychron / NuPhy / Akko Keyboard", "Keyboard", Array.Empty<string>(), new[] { "Keychron", "Lemokey", "NuPhy", "Nuphy", "Akko", "Epomaker", "Aula F75", "Royal Kludge", "RK61", "RK84", "Anne Pro", "GMMK" }, true, "Keyboard stays passive-only."),

        // Controllers.
        Profile("Xbox Wireless Controller", "Controller", Array.Empty<string>(), new[] { "Xbox Wireless Controller", "Xbox Controller", "Xbox Elite Wireless Controller", "Xbox Elite Series 2" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Sony DualSense Controller", "Controller", Array.Empty<string>(), new[] { "DualSense", "DualSense Edge" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Sony DualShock 4", "Controller", Array.Empty<string>(), new[] { "DualShock", "DUALSHOCK" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Nintendo Switch Pro Controller", "Controller", Array.Empty<string>(), new[] { "Switch Pro Controller", "Nintendo Switch Pro", "Joy-Con" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("8BitDo / Flydigi / GuliKit Controller", "Controller", Array.Empty<string>(), new[] { "8BitDo", "8Bitdo", "Flydigi", "Apex 4", "Vader 3", "Vader 4", "GuliKit", "Gulikit", "KingKong", "SCUF", "Victrix" }, true, "Windows/Bluetooth battery support when exposed."),

        // Bluetooth headphones and earbuds.
        Profile("Apple AirPods", "Headset", Array.Empty<string>(), new[] { "AirPods", "AirPods Pro", "AirPods Max" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Sony Bluetooth Headphones", "Headset", Array.Empty<string>(), new[] { "WH-1000", "WF-1000", "Sony WH", "Sony WF", "INZONE" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Bose Bluetooth Headphones", "Headset", Array.Empty<string>(), new[] { "Bose QC", "Bose QuietComfort", "Bose 700", "Bose Ultra" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Sennheiser / Momentum", "Headset", Array.Empty<string>(), new[] { "Sennheiser", "Momentum", "Accentum" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("JBL / Beats / Jabra / Soundcore", "Headset", Array.Empty<string>(), new[] { "JBL Live", "JBL Tune", "JBL Quantum", "Beats", "Jabra", "Soundcore", "Anker Soundcore", "Nothing Ear", "Galaxy Buds", "Pixel Buds", "Shokz", "Edifier" }, true, "Windows/Bluetooth battery support when exposed."),

        // Generic Bluetooth categories. These are last and passive only.
        Profile("Bluetooth headphones", "Headset", Array.Empty<string>(), new[] { "Bluetooth Headset", "Bluetooth Headphones", "Bluetooth Earbuds", "Hands-Free AG Audio" }, true, "Windows/Bluetooth battery support when exposed."),
        Profile("Bluetooth keyboard", "Keyboard", Array.Empty<string>(), new[] { "Bluetooth Keyboard" }, true, "Keyboard stays passive-only; battery appears if Windows exposes it."),
        Profile("Bluetooth mouse", "Mouse", Array.Empty<string>(), new[] { "Bluetooth Mouse", "Surface Mouse", "Magic Mouse", "Magic Trackpad" }, true, "Windows/Bluetooth battery support when exposed.")
    };

    private static DeviceProfile Profile(string name, string type, IEnumerable<string> ids, IEnumerable<string> aliases, bool passiveOnly, string notes) => new()
    {
        DisplayName = name,
        DeviceType = type,
        HardwareIds = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        Aliases = aliases.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        PassiveOnly = passiveOnly,
        ShowWhenDetectedWithoutBattery = true,
        Notes = notes
    };
}
