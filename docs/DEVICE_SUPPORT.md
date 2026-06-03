# Enhanced Device Support Guide

## Overview

This improved version supports a much broader range of wireless peripherals while maintaining the strict safety rule: **no generic HID probing**, **no commands sent to keyboards**, and **no interference with input devices**.

## Supported Device Categories

### 🎮 Gaming Peripherals

#### Targeted Direct Readers (Highest Priority)
- **Razer Viper V2 Pro** - Direct HID frame reading (VID_1532&PID_00A6, VID_1532&PID_00A5)
- **Logitech G733** - Direct HID frame reading (VID_046D&PID_0AB5)

#### Enhanced Recognition (Passive Detection)
All devices in this category are detected passively through Windows battery properties:

| Brand | Devices | Connection | Battery Support |
|-------|---------|-----------|-----------------|
| **Razer** | DeathAdder V3, Naga Pro, Basilisk, Viper Mini, Pro, Ultimate | 2.4GHz Dongle, Bluetooth | Full support |
| **Logitech G** | G502, G703, G915, G733, G435, G PRO X, G PRO X 2 | Lightspeed, Bluetooth | Full support |
| **SteelSeries** | Arctis, Prime, Alias, Rival, Aerox, Nova, NOVA ULTRA | 2.4GHz, Bluetooth | Full support |
| **HyperX** | Cloud Stinger, Alloy Elite, Pulsefire | 2.4GHz, Bluetooth | Supported if exposed |
| **CORSAIR** | DARK CORE, M65, SCIMITAR, VOID, VIRTUOSO | 2.4GHz, Bluetooth | Full support |
| **ASUS ROG** | Chakram, Strix, Keris | 2.4GHz, Bluetooth | Supported if exposed |
| **Turtle Beach** | Stealth, Battle Buds, Velocity One | 2.4GHz, Bluetooth | Full support |

### 🎧 Audio & Communication

| Device Type | Brands | Support |
|-------------|--------|---------|
| **Gaming Headsets** | All major brands using Bluetooth/2.4GHz | Full |
| **Wireless Earbuds** | AirPods, Galaxy Buds, WH-1000XM4, etc. | Full |
| **Bluetooth Headphones** | Sony, Sennheiser, Bose, JBL | Full |
| **Wireless Microphones** | Audio-Technica, Shure, rode (wireless) | Supported if exposed |

### 🖥️ Input Devices

| Device Type | Support | Notes |
|-------------|---------|-------|
| **Wireless Mice** | All major brands | Full passive support |
| **Wireless Keyboards** | All major brands | Passive detection only - no active probing |
| **QwertyKey Keyboard** | VID_36B0&PID_3002 | Recognized but never actively probed |
| **Input Dongles** | All wireless receiver dongles | Visible for diagnostics only |

### 🎮 Game Controllers

| Controller | Connection | Support |
|-----------|-----------|---------|
| **Xbox Series X/S Controller** | Bluetooth, 2.4GHz (with adapter) | Full |
| **Xbox One Controller** | Bluetooth, USB | Full |
| **PlayStation 5 (DualSense)** | Bluetooth | Full |
| **PlayStation 4 (DualShock 4)** | Bluetooth, USB | Full |
| **Nintendo Switch Pro** | Bluetooth | Full |
| **8BitDo Controllers** | Bluetooth, 2.4GHz | Full |

### ⌚ Wearables & Accessories

| Category | Examples | Support |
|----------|----------|---------|
| **Smartwatches** | Apple Watch, Wear OS, Fitbit, Xiaomi | Full (if Windows exposes battery) |
| **Fitness Trackers** | Fitbit, Garmin, Mi Band | Full (if Windows exposes battery) |
| **Smart Rings** | Oura Ring | Full (if Windows exposes battery) |
| **Portable Chargers** | Wireless power banks | Supported if exposed as battery |

### 🖥️ System Battery

- **Laptop/Tablet Battery** - Always shown if available
- **Windows System Battery Status** - Passive monitoring

## Detection Methods (In Order of Priority)

### 1. **Targeted Direct Readers** (Most Reliable)
```
✓ Razer Viper V2 Pro: Active HID frame reading
✓ Logitech G733: Active HID frame reading
✓ Both are isolated - only these exact VID/PIDs are probed
```

### 2. **Passive Windows Battery Properties** (Safe)
```
✓ Reads Windows PnP battery status
✓ No commands sent to devices
✓ Works with any USB/Bluetooth device that exposes battery
```

### 3. **Known Device Hints** (User-Configured)
```
✓ Users can add hints by brand/model/VID/PID
✓ App learns to recognize new devices
✓ No active probing
```

### 4. **Wireless Peripheral Pattern Matching** (Automatic)
```
✓ Bluetooth device detection
✓ Wireless receiver recognition
✓ Dongle/2.4GHz identifier matching
```

## What DOES NOT Get Probed

❌ **Keyboards** - Never actively scanned or queried
❌ **Unknown HID devices** - No generic scanning
❌ **Wired USB devices** - Unless they expose battery properties
❌ **Monitors, USB Hubs, Storage** - Explicitly filtered
❌ **Audio inputs/outputs** - Unless they have battery


## Expanding Support for New Devices

### For Users

1. **Check Windows Device Manager**
   - Find your device's VID/PID
   - Example: `VID_046D&PID_0AB5` for Logitech G733

2. **Add Device Hint in App**
   - Settings → Devices tab → Device Hints
   - Add your brand or VID/PID
   - No recompile needed!

3. **Enable Passive Discovery**
   - Most devices just need to be enabled in Windows
   - The app will find them automatically

### For Developers

1. **No new readers needed for most devices**
   - If Windows reports battery percentage, the app shows it
   - Most gaming peripherals now work out of the box

2. **To add a targeted direct reader**
   - Create new reader in `Readers/` folder
   - Implement `IBatteryReader` interface
   - Add VID/PID safety guards
   - **Important:** Only probe exact known IDs

## Safety Rules (Non-Negotiable)

✅ **Always followed:**
- No generic HID device scanning
- No keyboard probing or interference
- No active commands to unknown devices
- Isolated, targeted readers only
- Timeouts on all reader operations
- Strict VID/PID filtering

❌ **Never done:**
- Generic HID enumeration
- Keyboard input simulation
- Dongle interrupt hijacking
- Unsolicited device querying

## Troubleshooting Device Detection

### Device not showing up?

1. **Check Windows Device Manager**
   - Device must appear in "Human Interface Devices" or "Batteries"
   - Verify it has a VID/PID

2. **Enable in Windows**
   - Some devices need Bluetooth pairing first
   - Some need driver installation
   - Once in Device Manager, the app sees it

3. **Add a device hint**
   - Settings → Devices tab
   - Add your brand name or VID/PID
   - Helps the app recognize it as wireless

4. **Check logs**
   - %APPDATA%\UniversalBatteryOverlay\logs
   - See exactly what the app detects

5. **Report the device**
   - Use GitHub issue template "Device Support Request"
   - Include: device name, brand, VID/PID, connection type, logs

## Reporting New Device Support

When requesting support for a new device:

1. **Device Information**
   - Full product name and model
   - Brand
   - Connection: Bluetooth or 2.4GHz dongle?

2. **VID/PID Details**
   - Found in Windows Device Manager → Properties → Hardware IDs
   - Example: `VID_046D&PID_0AB5`

3. **Windows Exposure**
   - Does Windows Device Manager show it?
   - Is it in "Human Interface Devices"?
   - Does it have a battery listed?

4. **Latest Logs**
   - Attach `%APPDATA%\UniversalBatteryOverlay\logs\startup.log`
   - Shows exactly what the app detects

## Version History

### v28+ (Enhanced)
- ✨ Expanded SteelSeries, HyperX, CORSAIR recognition
- ✨ Improved wireless peripheral detection patterns
- ✨ Better device normalization and grouping
- ✨ More elegant overlay styling
- 🔒 Safety rules unchanged - still zero generic HID probing
- 🎯 Targeted readers still isolated to exact VID/PIDs

### Key Safety Commitment
This version maintains the **absolute safety rule**: zero interference with keyboard input, no generic HID scanning, and only targeted readers for exact known hardware. You can safely use this app for your gaming setup without worrying about input freezing or keyboard interference.


## Safety policy in this build

Universal Battery Overlay does not send commands to unknown HID devices. QwertyKey is passive-only. Active readers are exact VID/PID targeted.
