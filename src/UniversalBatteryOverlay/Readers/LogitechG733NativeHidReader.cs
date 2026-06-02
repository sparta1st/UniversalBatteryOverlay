using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Standalone Logitech HID++ reader. No Logitech G HUB dependency.
/// It tries the HID++ 2.0 Battery Unified feature directly over the G733 USB/HID interfaces.
/// </summary>
public sealed class LogitechG733NativeHidReader : IBatteryReader
{
    public string Name => "Logitech G733 native HID++";

    public Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<DeviceBatteryInfo>>(() =>
        {
            try
            {
                var paths = HidInterop.EnumerateHidPaths()
                    .Where(p => p.Contains("vid_046d", StringComparison.OrdinalIgnoreCase) &&
                                p.Contains("pid_0ab5", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryReadBattery(path, out var percent, out var details))
                    {
                        return new[]
                        {
                            new DeviceBatteryInfo
                            {
                                Name = "Logitech G733",
                                DeviceType = "Headset",
                                BatteryPercent = percent,
                                IsCharging = null,
                                Status = details,
                                Reader = Name,
                                RawId = path,
                                LastUpdated = DateTime.Now
                            }
                        };
                    }
                }

                return Array.Empty<DeviceBatteryInfo>();
            }
            catch
            {
                return Array.Empty<DeviceBatteryInfo>();
            }
        }, cancellationToken);
    }

    private static bool TryReadBattery(string path, out int percent, out string details)
    {
        percent = 0;
        details = "No HID++ battery response";

        using var handle = HidInterop.CreateFile(
            path,
            HidInterop.GENERIC_READ | HidInterop.GENERIC_WRITE,
            HidInterop.FILE_SHARE_READ | HidInterop.FILE_SHARE_WRITE,
            IntPtr.Zero,
            HidInterop.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            details = "HID open failed";
            return false;
        }

        // Direct USB HID++ usually accepts 0xFF. Receivers can expose paired devices
        // as 0x01..0x06, so try both paths.
        byte[] deviceIndexes = { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x00 };

        foreach (var deviceIndex in deviceIndexes)
        {
            if (TryGetFeatureIndex(handle, deviceIndex, 0x1000, out var batteryFeatureIndex))
            {
                if (TryGetBatteryFromFeature(handle, deviceIndex, batteryFeatureIndex, out percent, out details))
                    return true;
            }
        }

        // Some firmware versions expose battery status on a known feature index even
        // when root feature discovery fails. Try a safe range.
        foreach (var deviceIndex in deviceIndexes)
        {
            for (byte featureIndex = 1; featureIndex <= 16; featureIndex++)
            {
                if (TryGetBatteryFromFeature(handle, deviceIndex, featureIndex, out percent, out details))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetFeatureIndex(SafeFileHandle handle, byte deviceIndex, ushort featureId, out byte featureIndex)
    {
        featureIndex = 0;
        var hi = (byte)(featureId >> 8);
        var lo = (byte)(featureId & 0xFF);

        // HID++ 2.0 Root feature index is 0. Function 0 asks for a feature index.
        var request = new byte[] { 0x10, deviceIndex, 0x00, 0x00, hi, lo, 0x00 };
        if (!TryTransact(handle, request, out var response)) return false;

        if (response.Length >= 6 && response[0] == 0x10 && response[2] == 0x00)
        {
            var candidate = response[4];
            if (candidate > 0 && candidate < 0x80)
            {
                featureIndex = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBatteryFromFeature(SafeFileHandle handle, byte deviceIndex, byte featureIndex, out int percent, out string details)
    {
        percent = 0;
        details = "No HID++ battery response";

        // Function 0 usually means get_status for Battery Unified.
        // Also try function 1 and 2 because some Logitech firmwares vary.
        byte[] functionIds = { 0x00, 0x10, 0x20 };
        foreach (var functionId in functionIds)
        {
            var request = new byte[] { 0x10, deviceIndex, featureIndex, functionId, 0x00, 0x00, 0x00 };
            if (!TryTransact(handle, request, out var response)) continue;

            var candidate = ExtractLikelyPercent(response);
            if (candidate.HasValue)
            {
                percent = candidate.Value;
                details = $"OK · HID++ feature 0x{featureIndex:X2}, fn 0x{functionId:X2}, raw {BitConverter.ToString(response)}";
                return true;
            }
        }

        return false;
    }

    private static int? ExtractLikelyPercent(byte[] response)
    {
        if (response.Length < 7 || response[0] != 0x10) return null;

        // HID++ params begin around byte 4. For Battery Unified, the percentage is
        // commonly one of the first parameter bytes. Prefer byte 5, then 4/6/7.
        int[] priority = { 5, 4, 6, 7, 8, 9 };
        foreach (var index in priority)
        {
            if (index >= response.Length) continue;
            var value = response[index];
            if (value <= 100 && value >= 3) return value;
        }

        // If the device is extremely low, accept 0..2 only when no other byte looks
        // like a normal status or feature index. This avoids many false positives.
        foreach (var index in priority)
        {
            if (index >= response.Length) continue;
            var value = response[index];
            if (value <= 2) return value;
        }

        return null;
    }

    private static bool TryTransact(SafeFileHandle handle, byte[] shortRequest, out byte[] response)
    {
        response = Array.Empty<byte>();

        // Try SetFeature/GetFeature first. It is safe and does not require a running
        // background reader thread.
        var featureReq = new byte[20];
        Array.Copy(shortRequest, featureReq, Math.Min(shortRequest.Length, featureReq.Length));
        _ = HidInterop.HidD_SetFeature(handle, featureReq, featureReq.Length);
        Thread.Sleep(25);

        var featureResp = new byte[20];
        featureResp[0] = 0x10;
        if (HidInterop.HidD_GetFeature(handle, featureResp, featureResp.Length) && LooksLikeHidppResponse(featureResp))
        {
            response = featureResp;
            return true;
        }

        // Then try output/input reports, which many Logitech HID++ devices use.
        var outputReq = new byte[20];
        Array.Copy(shortRequest, outputReq, Math.Min(shortRequest.Length, outputReq.Length));
        _ = HidInterop.HidD_SetOutputReport(handle, outputReq, outputReq.Length);
        Thread.Sleep(25);

        var inputResp = new byte[20];
        inputResp[0] = 0x10;
        if (HidInterop.HidD_GetInputReport(handle, inputResp, inputResp.Length) && LooksLikeHidppResponse(inputResp))
        {
            response = inputResp;
            return true;
        }

        // Last: try the exact short report size.
        _ = HidInterop.HidD_SetOutputReport(handle, shortRequest, shortRequest.Length);
        Thread.Sleep(25);

        var shortResp = new byte[7];
        shortResp[0] = 0x10;
        if (HidInterop.HidD_GetInputReport(handle, shortResp, shortResp.Length) && LooksLikeHidppResponse(shortResp))
        {
            response = shortResp;
            return true;
        }

        return false;
    }

    private static bool LooksLikeHidppResponse(byte[] data)
        => data.Length >= 7 && (data[0] == 0x10 || data[0] == 0x11) && data.Any(b => b != 0);

    private static class HidInterop
    {
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
        public static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetOutputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

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
