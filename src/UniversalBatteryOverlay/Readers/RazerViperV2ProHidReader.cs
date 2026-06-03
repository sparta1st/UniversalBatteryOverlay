using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UniversalBatteryOverlay.Models;

namespace UniversalBatteryOverlay.Readers;

/// <summary>
/// Razer Viper V2 Pro battery reader, Windows-safe edition.
///
/// Why v11 is different:
/// - It opens the HID handle with dwDesiredAccess = 0, like the small Windows Viper tray projects do.
///   This avoids the normal mouse/keyboard exclusive lock and avoids grabbing input control.
/// - It filters by HID caps: UsagePage 0x01, Usage 0x02, FeatureReportByteLength >= 90/91.
///   This targets the mouse collection, not keyboard/media collections.
/// - It tries transaction IDs 0x1F first, then 0x3F, because newer public Windows Razer tray code
///   uses 0x1F while the older Viper V2 Pro PyUSB script used 0x3F.
/// - It sends only the Razer GET_BATTERY command_class=0x07 command_id=0x80.
/// </summary>
public sealed class RazerViperV2ProHidReader : IBatteryReader
{
    private static readonly SemaphoreSlim DeviceLock = new(1, 1);
    private static readonly byte[] TransactionIds = { 0x1f, 0x3f, 0xff };

    public string Name => "Razer Viper V2 Pro zero-access HID battery";

    public async Task<IReadOnlyList<DeviceBatteryInfo>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await DeviceLock.WaitAsync(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false))
            return Array.Empty<DeviceBatteryInfo>();

        try
        {
            return await Task.Run<IReadOnlyList<DeviceBatteryInfo>>(() =>
            {
                var lastStatus = "No Razer HID path found";
                var lastPath = string.Empty;

                try
                {
                    var candidates = HidInterop.EnumerateHidPaths()
                        .Where(IsRazerViperPath)
                        .Select(path => ProbeCandidate(path))
                        .Where(x => x.IsUsable)
                        .OrderByDescending(x => x.Score)
                        .Take(10)
                        .ToList();

                    foreach (var c in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lastPath = c.Path;

                        if (TryReadBattery(c, out var percent, out var raw, out var status))
                        {
                            return new[]
                            {
                                new DeviceBatteryInfo
                                {
                                    Name = "Razer Viper V2 Pro",
                                    DeviceType = "Mouse",
                                    BatteryPercent = percent,
                                    IsCharging = null,
                                    Status = status,
                                    Reader = Name,
                                    RawId = c.Path,
                                    LastUpdated = DateTime.Now
                                }
                            };
                        }

                        lastStatus = status;
                    }

                    if (candidates.Count > 0)
                    {
                        return new[]
                        {
                            new DeviceBatteryInfo
                            {
                                Name = "Razer Viper V2 Pro",
                                DeviceType = "Mouse",
                                BatteryPercent = null,
                                IsCharging = null,
                                Status = "Targeted read failed: " + lastStatus,
                                Reader = Name,
                                RawId = lastPath,
                                LastUpdated = DateTime.Now
                            }
                        };
                    }

                    // If VID/PID exists but caps filtering rejected it, expose a helpful status.
                    var rawPaths = HidInterop.EnumerateHidPaths().Where(IsRazerViperPath).Take(4).ToList();
                    if (rawPaths.Count > 0)
                    {
                        return new[]
                        {
                            new DeviceBatteryInfo
                            {
                                Name = "Razer Viper V2 Pro",
                                DeviceType = "Mouse",
                                BatteryPercent = null,
                                IsCharging = null,
                                Status = "Razer paths found, but no mouse feature-report collection accepted. Run as admin/reconnect dongle.",
                                Reader = Name,
                                RawId = rawPaths[0],
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

    private static bool IsRazerViperPath(string path)
    {
        var p = path.ToLowerInvariant();
        return p.Contains("vid_1532&pid_00a6") || p.Contains("vid_1532&pid_00a5");
    }

    private static Candidate ProbeCandidate(string path)
    {
        var score = ScorePathFromName(path);
        ushort usagePage = 0, usage = 0, featureLen = 0, inputLen = 0, outputLen = 0;
        var capsOk = false;
        var openOk = false;

        using (var handle = HidInterop.OpenFeatureOnly(path))
        {
            openOk = !handle.IsInvalid;
            if (openOk && HidInterop.TryGetCaps(handle, out usagePage, out usage, out featureLen, out inputLen, out outputLen))
            {
                capsOk = true;
                if (usagePage == 0x01) score += 200;
                if (usage == 0x02) score += 500;          // mouse collection
                if (usage == 0x06 || usage == 0x80) score -= 500; // keyboard/system control collections
                if (featureLen >= 91) score += 700;
                else if (featureLen >= 90) score += 350;
                else score -= 700;
            }
        }

        var isUsable = openOk && capsOk && featureLen >= 90 && usagePage == 0x01 && usage == 0x02;
        return new Candidate(path, score, isUsable, usagePage, usage, featureLen, inputLen, outputLen);
    }

    private static int ScorePathFromName(string path)
    {
        var p = path.ToLowerInvariant();
        var score = 0;
        if (p.Contains("vid_1532&pid_00a6")) score += 1000; // wireless receiver
        if (p.Contains("vid_1532&pid_00a5")) score += 900;  // wired mode
        if (p.Contains("mi_00")) score += 200;
        if (p.Contains("mi_02")) score += 150;
        if (p.Contains("mi_01")) score -= 100;
        if (p.Contains("col01")) score += 40;
        if (p.Contains("col02")) score += 20;
        if (p.Contains("kbd")) score -= 500;
        return score;
    }

    private static bool TryReadBattery(Candidate c, out int percent, out int raw, out string status)
    {
        percent = 0;
        raw = 0;
        status = "not attempted";

        using var handle = HidInterop.OpenFeatureOnly(c.Path);
        if (handle.IsInvalid)
        {
            status = "cannot open zero-access HID handle";
            return false;
        }

        var caps = $"caps UP=0x{c.UsagePage:X} U=0x{c.Usage:X} F={c.FeatureLen} I={c.InputLen} O={c.OutputLen}";
        var lengths = new List<int>();
        if (c.FeatureLen >= 90 && c.FeatureLen <= 256) lengths.Add(c.FeatureLen);
        // Windows HidD_* often needs a leading report-id slot, so 91 is the most important one.
        lengths.AddRange(new[] { 91, 90 });
        lengths = lengths.Where(x => x >= 90 && x <= 256).Distinct().ToList();

        foreach (var tranId in TransactionIds)
        {
            foreach (var len in lengths)
            {
                foreach (var mode in ReportBuildModeExtensions.PreferenceOrderFor(len))
                {
                    var request = BuildBatteryReport(tranId, len, mode);
                    if (request is null) continue;

                    // Clear last error before each HID call for better diagnostics.
                    Marshal.GetLastWin32Error();
                    if (!HidInterop.HidD_SetFeature(handle, request, request.Length))
                    {
                        status = $"SetFeature failed tran=0x{tranId:X2}, len={len}, mode={mode}, err={Marshal.GetLastWin32Error()}, {caps}";
                        continue;
                    }

                    Thread.Sleep(340);

                    var response = new byte[len];
                    response[0] = 0x00;
                    if (!HidInterop.HidD_GetFeature(handle, response, response.Length))
                    {
                        status = $"GetFeature failed tran=0x{tranId:X2}, len={len}, mode={mode}, err={Marshal.GetLastWin32Error()}, {caps}";
                        continue;
                    }

                    if (TryExtractBattery(response, out raw, out percent))
                    {
                        status = $"OK · {percent}% · raw {raw}/255 · tran=0x{tranId:X2} · len={len} · mode={mode} · {caps}";
                        return true;
                    }

                    status = $"response without battery tran=0x{tranId:X2}, len={len}, mode={mode}, raw={BitConverter.ToString(response.Take(Math.Min(24, response.Length)).ToArray())}, {caps}";
                }
            }
        }

        return false;
    }

    private static bool TryExtractBattery(byte[] response, out int raw, out int percent)
    {
        raw = 0;
        percent = 0;

        // Known OpenRazer/PyUSB report has battery at raw index 9. With Windows report-id slot it lands at 10.
        foreach (var idx in new[] { 10, 9, 11, 8 })
        {
            if (idx < 0 || idx >= response.Length) continue;
            var value = (int)response[idx];
            if (value <= 0) continue;
            if (value is 0x80 or 0x3f or 0x1f or 0xff) continue;

            // Accept only if the surrounding frame still looks like a Razer response.
            var looksRazer = response.Take(Math.Min(16, response.Length)).Any(b => b == 0x07)
                             || response.Take(Math.Min(16, response.Length)).Any(b => b == 0x80);
            if (!looksRazer && idx is not (9 or 10)) continue;

            raw = value;
            percent = Math.Clamp((int)Math.Round(value / 255.0 * 100.0), 1, 100);
            return true;
        }

        return false;
    }

    private static byte[]? BuildBatteryReport(byte transactionId, int length, ReportBuildMode mode)
    {
        // PyUSB/OpenRazer 90-byte payload:
        // status, transaction, remaining_packets(2), protocol_type, data_size, command_class=0x07, command_id=0x80, 80 data bytes, crc, 0.
        var payload = new byte[90];
        payload[0] = 0x00;
        payload[1] = transactionId;
        payload[2] = 0x00;
        payload[3] = 0x00;
        payload[4] = 0x00;
        payload[5] = 0x02;
        payload[6] = 0x07;
        payload[7] = 0x80;

        byte crc = 0;
        for (var i = 2; i < 88; i++) crc ^= payload[i];
        payload[88] = crc;
        payload[89] = 0x00;

        if (mode == ReportBuildMode.Raw90)
        {
            if (length < 90) return null;
            var buffer = new byte[length];
            Array.Copy(payload, 0, buffer, 0, payload.Length);
            return buffer;
        }

        if (mode == ReportBuildMode.WindowsReportId0ThenPayload)
        {
            if (length < 91) return null;
            var buffer = new byte[length];
            buffer[0] = 0x00;
            Array.Copy(payload, 0, buffer, 1, payload.Length);
            return buffer;
        }

        if (mode == ReportBuildMode.WindowsReportId0PayloadWithoutStatus)
        {
            if (length < 90) return null;
            var buffer = new byte[length];
            buffer[0] = 0x00;
            // If HidD treats byte 0 as report ID, some devices expect the 89 bytes after the status byte.
            Array.Copy(payload, 1, buffer, 1, payload.Length - 1);
            return buffer;
        }

        return null;
    }

    private readonly record struct Candidate(
        string Path,
        int Score,
        bool IsUsable,
        ushort UsagePage,
        ushort Usage,
        ushort FeatureLen,
        ushort InputLen,
        ushort OutputLen);

    private enum ReportBuildMode
    {
        WindowsReportId0ThenPayload,
        Raw90,
        WindowsReportId0PayloadWithoutStatus
    }

    private static class ReportBuildModeExtensions
    {
        public static IEnumerable<ReportBuildMode> PreferenceOrderFor(int len)
        {
            if (len >= 91) yield return ReportBuildMode.WindowsReportId0ThenPayload;
            yield return ReportBuildMode.Raw90;
            yield return ReportBuildMode.WindowsReportId0PayloadWithoutStatus;
        }
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

        // DesiredAccess = 0 is intentional. It lets HidD feature reports work on mouse collections without
        // taking normal input read/write ownership, reducing interference risk.
        public static SafeFileHandle OpenFeatureOnly(string path) => CreateFile(
            path,
            0,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        public static SafeFileHandle OpenReadWrite(string path) => CreateFile(
            path,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        public static bool TryGetCaps(SafeFileHandle handle, out ushort usagePage, out ushort usage, out ushort featureLen, out ushort inputLen, out ushort outputLen)
        {
            usagePage = usage = featureLen = inputLen = outputLen = 0;
            if (!HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero)
                return false;

            try
            {
                var result = HidP_GetCaps(preparsedData, out var caps);
                if (result != HIDP_STATUS_SUCCESS) return false;
                usagePage = caps.UsagePage;
                usage = caps.Usage;
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
        public static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

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
