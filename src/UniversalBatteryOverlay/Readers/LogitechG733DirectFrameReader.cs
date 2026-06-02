using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Logitech G733 reader without G HUB.
/// Uses the 20-byte G733 frames documented/reversed by the G733 Windows companion project:
/// Battery write: 11 FF 08 0E ...
/// Battery read:  11 FF 08 0E volt_MSB volt_LSB state ...
/// This reader only opens Logitech VID_046D&PID_0AB5 paths. It never touches the keyboard.
/// </summary>
public sealed class LogitechG733DirectFrameReader : IBatteryReader
{
    private static readonly SemaphoreSlim DeviceLock = new(1, 1);
    public string Name => "Logitech G733 direct frame battery";

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await DeviceLock.WaitAsync(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false))
            return Array.Empty<DeviceBatteryInfo>();

        try
        {
            return await Task.Run<IReadOnlyList<DeviceBatteryInfo>>(() =>
            {
                var lastStatus = "No G733 HID path found";
                var lastPath = string.Empty;

                try
                {
                    var paths = HidInterop.EnumerateHidPaths()
                        .Where(IsG733Path)
                        .Select(p => new { Path = p, Score = ScorePath(p) + ScoreCaps(p) })
                        .OrderByDescending(x => x.Score)
                        .Select(x => x.Path)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(24)
                        .ToList();

                    foreach (var path in paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lastPath = path;

                        if (TryReadBattery(path, out var percent, out var voltage, out var charging, out var status))
                        {
                            return new[]
                            {
                                new DeviceBatteryInfo
                                {
                                    Name = "Logitech G733",
                                    DeviceType = "Headset",
                                    BatteryPercent = percent,
                                    IsCharging = charging,
                                    Status = status,
                                    Reader = Name,
                                    RawId = path,
                                    LastUpdated = DateTime.Now
                                }
                            };
                        }

                        lastStatus = status;
                    }

                    if (paths.Count > 0)
                    {
                        return new[]
                        {
                            new DeviceBatteryInfo
                            {
                                Name = "Logitech G733",
                                DeviceType = "Headset",
                                BatteryPercent = null,
                                IsCharging = null,
                                Status = "Direct G733 read failed: " + lastStatus,
                                Reader = Name,
                                RawId = lastPath,
                                LastUpdated = DateTime.Now
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    lastStatus = ex.GetType().Name + ": " + ex.Message;
                }

                return Array.Empty<DeviceBatteryInfo>();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeviceLock.Release();
        }
    }

    private static bool IsG733Path(string path)
    {
        var p = path.ToLowerInvariant();
        return p.Contains("vid_046d&pid_0ab5");
    }

    private static int ScorePath(string path)
    {
        var p = path.ToLowerInvariant();
        var score = 1000;
        if (p.Contains("mi_03")) score += 300; // G733 vendor-defined collections are usually here.
        if (p.Contains("col04")) score += 120;
        if (p.Contains("col03")) score += 90;
        if (p.Contains("col02")) score += 40;
        if (p.Contains("vendor")) score += 30;
        if (p.Contains("mi_00")) score -= 20; // audio/media interface usually cannot answer HID++ battery frames.
        return score;
    }

    private static int ScoreCaps(string path)
    {
        try
        {
            using var handle = HidInterop.OpenReadWrite(path);
            if (handle.IsInvalid) return -200;
            if (!HidInterop.TryGetCaps(handle, out var featureLen, out var inputLen, out var outputLen)) return 0;

            var score = 0;
            if (outputLen >= 20) score += 500;
            if (featureLen >= 20) score += 350;
            if (inputLen >= 20) score += 250;
            if (inputLen == 5 && outputLen == 0 && featureLen == 0) score -= 400;
            return score;
        }
        catch
        {
            return 0;
        }
    }

    private static bool TryReadBattery(string path, out int percent, out int voltageMv, out bool? charging, out string status)
    {
        percent = 0;
        voltageMv = 0;
        charging = null;
        status = "not attempted";

        using var handle = HidInterop.OpenReadWrite(path);
        if (handle.IsInvalid)
        {
            status = "cannot open handle";
            return false;
        }

        var caps = HidInterop.TryGetCaps(handle, out var featureLen, out var inputLen, out var outputLen)
            ? $"caps F={featureLen} I={inputLen} O={outputLen}"
            : "caps unavailable";

        var requests = BuildRequests(outputLen, featureLen).ToList();
        foreach (var request in requests)
        {
            // G733 battery request is an output report. Try the three safe Windows paths:
            // SetOutputReport, WriteFile, then Feature as fallback. This targets only VID_046D/PID_0AB5.
            var sentMethod = HidInterop.TrySendReport(handle, request);
            if (sentMethod is null)
            {
                status = $"send failed len={request.Length}, err={Marshal.GetLastWin32Error()}, {caps}";
                continue;
            }

            Thread.Sleep(90);

            foreach (var responseLength in CandidateResponseLengths(inputLen, featureLen))
            {
                var response = new byte[responseLength];
                response[0] = 0x11;

                var ok = HidInterop.HidD_GetInputReport(handle, response, response.Length);
                if (!ok)
                {
                    response[0] = 0x11;
                    ok = HidInterop.HidD_GetFeature(handle, response, response.Length);
                }

                if (!ok)
                {
                    status = $"read failed len={responseLength}, err={Marshal.GetLastWin32Error()}, {caps}";
                    continue;
                }

                if (TryParseG733Battery(response, out voltageMv, out charging, out percent))
                {
                    var state = charging == true ? "charging" : charging == false ? "discharging" : "unknown";
                    status = $"OK · {percent}% · {voltageMv}mV · {state} · sent={sentMethod} · {caps}";
                    return true;
                }

                status = $"response without battery raw={BitConverter.ToString(response.Take(Math.Min(20, response.Length)).ToArray())}, {caps}";
            }
        }

        return false;
    }

    private static IEnumerable<byte[]> BuildRequests(ushort outputLen, ushort featureLen)
    {
        var base20 = new byte[20];
        base20[0] = 0x11;
        base20[1] = 0xff;
        base20[2] = 0x08;
        base20[3] = 0x0e;

        var lengths = new List<int> { 20, 21 };
        if (outputLen >= 20 && outputLen <= 128) lengths.Insert(0, outputLen);
        if (featureLen >= 20 && featureLen <= 128) lengths.Add(featureLen);

        foreach (var len in lengths.Distinct())
        {
            if (len < 20) continue;
            var b = new byte[len];
            Array.Copy(base20, 0, b, 0, base20.Length);
            yield return b;

            // Windows HID shifted variant: byte 0 report-id slot, then 20-byte frame.
            if (len >= 21)
            {
                var shifted = new byte[len];
                shifted[0] = 0x00;
                Array.Copy(base20, 0, shifted, 1, base20.Length);
                yield return shifted;
            }
        }
    }

    private static IEnumerable<int> CandidateResponseLengths(ushort inputLen, ushort featureLen)
    {
        var lengths = new List<int> { 20, 21, 64 };
        if (inputLen >= 20 && inputLen <= 128) lengths.Insert(0, inputLen);
        if (featureLen >= 20 && featureLen <= 128) lengths.Add(featureLen);
        return lengths.Distinct();
    }

    private static bool TryParseG733Battery(byte[] data, out int voltageMv, out bool? charging, out int percent)
    {
        voltageMv = 0;
        charging = null;
        percent = 0;

        foreach (var offset in new[] { 0, 1 })
        {
            if (data.Length < offset + 7) continue;
            if (data[offset + 0] != 0x11) continue;
            if (data[offset + 1] != 0xff) continue;
            if (data[offset + 2] != 0x08) continue;
            if (data[offset + 3] != 0x0e) continue;

            voltageMv = (data[offset + 4] << 8) | data[offset + 5];
            var state = data[offset + 6];
            charging = state switch
            {
                0x01 => false,
                0x03 => true,
                0x07 => true,
                _ => null
            };

            if (voltageMv is < 3200 or > 4400) continue;
            percent = EstimateLiIonPercent(voltageMv);
            return true;
        }

        return false;
    }

    private static int EstimateLiIonPercent(int mv)
    {
        // Conservative single-cell Li-ion voltage estimate. G733 reversed frames expose voltage, not a guaranteed SOC %.
        // Points are chosen to avoid showing absurd values. G HUB itself also estimates/calibrates G733 percentage.
        (int mv, int percent)[] curve =
        {
            (4200, 100), (4150, 95), (4100, 90), (4050, 85), (4000, 78),
            (3950, 70), (3900, 60), (3850, 50), (3800, 40), (3750, 30),
            (3700, 22), (3650, 15), (3600, 10), (3550, 6), (3500, 3), (3400, 0)
        };

        if (mv >= curve[0].mv) return 100;
        if (mv <= curve[^1].mv) return 0;

        for (var i = 0; i < curve.Length - 1; i++)
        {
            var high = curve[i];
            var low = curve[i + 1];
            if (mv <= high.mv && mv >= low.mv)
            {
                var t = (mv - low.mv) / (double)(high.mv - low.mv);
                return Math.Clamp((int)Math.Round(low.percent + t * (high.percent - low.percent)), 0, 100);
            }
        }

        return Math.Clamp((mv - 3400) * 100 / 800, 0, 100);
    }

    private static class HidInterop
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int HIDP_STATUS_SUCCESS = 0x00110000;

        public static SafeFileHandle OpenReadWrite(string path) => CreateFile(
            path,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        public static bool TryGetCaps(SafeFileHandle handle, out ushort featureLen, out ushort inputLen, out ushort outputLen)
        {
            featureLen = inputLen = outputLen = 0;
            if (!HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero)
                return false;

            try
            {
                var result = HidP_GetCaps(preparsedData, out var caps);
                if (result != HIDP_STATUS_SUCCESS) return false;
                featureLen = caps.FeatureReportByteLength;
                inputLen = caps.InputReportByteLength;
                outputLen = caps.OutputReportByteLength;
                return true;
            }
            finally
            {
                HidD_FreePreparsedData(preparsedData);
            }
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetOutputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        public static string? TrySendReport(SafeFileHandle handle, byte[] report)
        {
            if (HidD_SetOutputReport(handle, report, report.Length)) return "SetOutputReport";
            if (WriteFile(handle, report, (uint)report.Length, out var written, IntPtr.Zero) && written > 0) return $"WriteFile:{written}";
            if (HidD_SetFeature(handle, report, report.Length)) return "SetFeature";
            return null;
        }

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
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
