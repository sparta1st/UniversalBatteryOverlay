using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Pure native Windows HID reader. It does not use Logitech G HUB, Razer Synapse,
/// QwertyKey software, or any vendor background app. It only asks Windows/HID for
/// the standard Battery Strength usage when the device exposes it.
/// </summary>
public sealed class StandardHidBatteryReader : IBatteryReader
{
    private readonly Func<IReadOnlyList<string>> _getHints;
    public string Name => "Native Windows HID battery";

    public StandardHidBatteryReader(Func<IReadOnlyList<string>> getHints)
    {
        _getHints = getHints;
    }

    public Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<DeviceBatteryInfo>>(() =>
        {
            var results = new List<DeviceBatteryInfo>();

            try
            {
                var hints = _getHints()
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h.ToLowerInvariant())
                    .ToArray();

                var paths = HidInterop.EnumerateHidPaths()
                    .Where(path => ShouldTry(path, hints))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryReadStandardBattery(path, out var percent))
                    {
                        var info = DeviceFromPath(path, percent);
                        results.Add(info);
                    }
                }
            }
            catch
            {
                // Keep this reader isolated. A HID failure should never break the app.
            }

            return results;
        }, cancellationToken);
    }

    private static bool ShouldTry(string path, string[] hints)
    {
        var lower = path.ToLowerInvariant();

        // Exact IDs from the user's PC plus broad gaming peripheral vendors.
        // This reader only asks for standard HID battery reports; it does not send output/vendor commands.
        string[] knownVendors =
        {
            "vid_1532", // Razer
            "vid_046d", // Logitech
            "vid_1038", // SteelSeries
            "vid_1b1c", // Corsair
            "vid_0951", // HyperX/Kingston
            "vid_0b05", // ASUS/ROG
            "vid_0db0", // MSI
            "vid_2516", // Cooler Master
            "vid_3434", // Keychron/QMK
            "vid_3297", // QMK/ZSA-like keyboards
            "vid_054c", // Sony
            "vid_045e", // Microsoft/Xbox
            "vid_057e", // Nintendo
            "vid_2dc8", // 8BitDo
            "bth",      // Bluetooth HID paths
            "vid_36b0"  // QwertyKey/user keyboard, read-only standard HID only
        };

        if (knownVendors.Any(lower.Contains)) return true;

        // User-configurable hints from settings.json.
        return hints.Any(h => lower.Contains(h.ToLowerInvariant()));
    }

    private DeviceBatteryInfo DeviceFromPath(string path, int percent)
    {
        var lower = path.ToLowerInvariant();
        var name = "HID battery device";
        var type = "Device";

        if (lower.Contains("vid_1532") && lower.Contains("pid_00a6"))
        {
            name = "Razer Viper V2 Pro";
            type = "Mouse";
        }
        else if (lower.Contains("vid_046d") && lower.Contains("pid_0ab5"))
        {
            name = "Logitech G733";
            type = "Headset";
        }
        else if (lower.Contains("vid_36b0") && lower.Contains("pid_3002"))
        {
            name = "QwertyKey Keyboard";
            type = "Keyboard";
        }
        else if (lower.Contains("vid_046d")) name = "Logitech HID battery device";
        else if (lower.Contains("vid_1532")) name = "Razer HID battery device";
        else if (lower.Contains("vid_1038")) name = "SteelSeries HID battery device";
        else if (lower.Contains("vid_1b1c")) name = "Corsair HID battery device";
        else if (lower.Contains("vid_0951")) name = "HyperX HID battery device";
        else if (lower.Contains("vid_0b05")) name = "ASUS/ROG HID battery device";
        else if (lower.Contains("vid_045e")) name = "Xbox/Microsoft HID battery device";
        else if (lower.Contains("vid_054c")) name = "Sony HID battery device";

        if (lower.Contains("mouse")) type = "Mouse";
        else if (lower.Contains("kbd") || lower.Contains("keyboard")) type = "Keyboard";
        else if (lower.Contains("headset") || lower.Contains("audio")) type = "Headset";

        return new DeviceBatteryInfo
        {
            Name = name,
            DeviceType = type,
            BatteryPercent = percent,
            IsCharging = null,
            Status = "OK · standard HID Battery Strength",
            Reader = Name,
            RawId = path,
            LastUpdated = DateTime.Now
        };
    }

    private static bool TryReadStandardBattery(string path, out int percent)
    {
        percent = 0;

        using var handle = HidInterop.CreateFile(
            path,
            HidInterop.GENERIC_READ,
            HidInterop.FILE_SHARE_READ | HidInterop.FILE_SHARE_WRITE,
            IntPtr.Zero,
            HidInterop.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid) return false;
        if (!HidInterop.HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero) return false;

        try
        {
            var capsStatus = HidInterop.HidP_GetCaps(preparsedData, out var caps);
            if (capsStatus != HidInterop.HIDP_STATUS_SUCCESS) return false;

            if (TryReadReportType(handle, preparsedData, HidpReportType.HidP_Feature, caps.FeatureReportByteLength, out percent)) return true;
            if (TryReadReportType(handle, preparsedData, HidpReportType.HidP_Input, caps.InputReportByteLength, out percent)) return true;
            return false;
        }
        finally
        {
            HidInterop.HidD_FreePreparsedData(preparsedData);
        }
    }

    private static bool TryReadReportType(SafeFileHandle handle, IntPtr preparsedData, HidpReportType reportType, ushort reportLength, out int percent)
    {
        percent = 0;
        if (reportLength < 2 || reportLength > 512) return false;

        // Most devices use small report IDs. Trying too many IDs can slow refresh,
        // so keep this conservative and safe.
        for (var reportId = 0; reportId <= 32; reportId++)
        {
            var report = new byte[reportLength];
            report[0] = (byte)reportId;

            var ok = reportType switch
            {
                HidpReportType.HidP_Feature => HidInterop.HidD_GetFeature(handle, report, report.Length),
                HidpReportType.HidP_Input => HidInterop.HidD_GetInputReport(handle, report, report.Length),
                _ => false
            };

            if (!ok) continue;

            // HID Usage Page 0x06 = Generic Device Controls,
            // Usage 0x20 = Battery Strength. This is the clean/standard path.
            if (HidInterop.HidP_GetUsageValue(reportType, 0x06, 0, 0x20, out var value, preparsedData, report, (uint)report.Length) == HidInterop.HIDP_STATUS_SUCCESS)
            {
                if (value <= 100)
                {
                    percent = (int)value;
                    return true;
                }
            }

            // Some devices expose battery under Power Device / Battery System pages.
            if (HidInterop.HidP_GetUsageValue(reportType, 0x85, 0, 0x66, out value, preparsedData, report, (uint)report.Length) == HidInterop.HIDP_STATUS_SUCCESS)
            {
                if (value <= 100)
                {
                    percent = (int)value;
                    return true;
                }
            }
        }

        return false;
    }

    public enum HidpReportType
    {
        HidP_Input = 0,
        HidP_Output = 1,
        HidP_Feature = 2
    }

    private static class HidInterop
    {
        public const int HIDP_STATUS_SUCCESS = 0x00110000;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        private const int ERROR_NO_MORE_ITEMS = 259;

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern int HidP_GetUsageValue(HidpReportType reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint usageValue, IntPtr preparsedData, byte[] report, uint reportLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        public struct HidpCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        public static IEnumerable<string> EnumerateHidPaths()
        {
            HidD_GetHidGuid(out var hidGuid);
            var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1)) yield break;

            try
            {
                for (uint index = 0; ; index++)
                {
                    var interfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                    {
                        if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_ITEMS) yield break;
                        continue;
                    }

                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
                    if (requiredSize == 0) continue;

                    var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
                            continue;

                        var pathPtr = IntPtr.Add(detailBuffer, 4);
                        var path = Marshal.PtrToStringAuto(pathPtr);
                        if (!string.IsNullOrWhiteSpace(path)) yield return path!;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
    }
}
