# FocusGlow — Windows 11 Focus Mode

A desktop focus application for Windows 11 built with C# (.NET 8) featuring ultra-smooth, hardware-accelerated animations inspired by macOS and Apple Intelligence (unlocked 120 / 240+ Hz refresh rate, no 60 FPS cap).

---

## Key Features

- **Apple Intelligence / macOS Optical Specular Glow:**
  A delicate, realistic optical light sweep rotates along the window perimeter with smooth opacity falloff at both ends. Powered by a pure pearl-white specular core (1.25 px) surrounded by subtle chromatic dispersion (ice blue, soft lavender, warm pearl) across three high-performance GPU bloom passes.
- **Two-Stage Ambient Dimming:**
  Everything outside the target window dims to pure OLED black: first a soft transition from 0% to 50% (~450 ms), followed by a deep, meditative fade from 50% to 100% (~1.5 s).
- **Fluid Cursor Reaction (Zero Jumps):**
  When your cursor leaves the active window, the dimming smoothly fades out to 0% (~220 ms), instantly granting full visibility. When the cursor returns, the dimming seamlessly resumes from the **current** opacity value (`current -> 50% -> 100%`) without visual cuts or stuttering.
- **Precision Window Tracking & 1px DWM Compensation:**
  Tracks window position, resizing, moving, and minimizing (`IsIconic`) in real time. Compensates for Windows 11 `DWMWA_EXTENDED_FRAME_BOUNDS` asymmetry so the overlay fits window edges with subpixel precision. Maximized windows automatically use a 0 px corner radius, while normal windows follow the Windows 11 8 px standard (scaled to monitor DPI).
- **Native Windows 11 Dark Minimalist UI:**
  Compact (360 × 248 px) settings window utilizing native Windows 11 Mica material (`DWMWA_SYSTEMBACKDROP_TYPE`) and `WindowChrome`, with pill-style shortcut recorder and subtle macOS-inspired sliders.
- **Global Input Hooks & System Tray:**
  Low-level keyboard and mouse hooks (`WH_KEYBOARD_LL` + `WH_MOUSE_LL`) support keyboard combinations (Ctrl, Alt, Shift, Win + key) as well as mouse side buttons (`XButton1` / `XButton2`). Pressing `Esc` or the hotkey smoothly deactivates focus mode.

---

## Technical Architecture

1. **GPU Rendering with SkiaSharp (`SkiaSharp.Views.WPF`, `SKElement`):**
   - Replaces WPF's CPU-bound `BlurEffect` and 60 FPS `Storyboard` engine with hardware-accelerated SkiaSharp canvas operations.
   - Smooth antialiased window cutout via `canvas.ClipRoundRect(..., SKClipOperation.Difference)`.
   - Zero-allocation per-frame rendering loop with reused path buffers.

2. **240+ FPS Animation Loop:**
   - Driven by `CompositionTarget.Rendering` with high-resolution `deltaTime` computed via `System.Diagnostics.Stopwatch.GetTimestamp()`.
   - Framerate unlocked globally using `Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(240))` to match native 120 Hz, 144 Hz, 240 Hz, or 360 Hz displays.

3. **Physics-Based Fluid Curves:**
   - Asymmetric deceleration curves: `cubic-bezier(0.22, 1, 0.36, 1)`.
   - Critical damped spring dynamics (`DampedSpring`) and smooth damp filters (`SmoothDamp`).
   - Integer pixel-snapping in resting state to eliminate subpixel anti-aliasing blur and gaps.

---

## Project Structure

```
FocusGlow/
├── FocusGlow.csproj            # .NET 8 WPF + WinForms (NotifyIcon) + SkiaSharp configuration
├── app.manifest                # Windows 11 compatibility & Per-Monitor V2 DPI manifest
├── App.xaml / App.xaml.cs      # Entry point, single-instance mutex, 240 Hz unlocked loop
│
├── Interop/
│   └── Win32.cs                # P/Invoke: DwmGetWindowAttribute, DwmSetWindowAttribute (Mica),
│                               # GetCursorPos, WindowFromPoint, GetAncestor, LowLevel Hooks
├── Physics/
│   └── AnimationPhysics.cs     # CubicBezier (0.22, 1, 0.36, 1), DampedSpring, SmoothDamp
│
├── Models/
│   └── AppConfig.cs            # Configuration model: Hotkey, Mouse buttons, Corner radius, Speed
├── Services/
│   └── ConfigManager.cs        # Thread-safe JSON configuration manager (config.json)
│
├── Rendering/
│   └── FocusOverlayRenderer.cs # SkiaSharp GPU renderer: Optical sweep gradient, 3-layer bloom,
│                               # Difference clip cutout, zero-allocation per frame
├── Core/
│   ├── FocusAnimationController.cs # Animation phases 1-3, continuous opacity, DWM 1px offset compensation
│   └── FocusManager.cs         # Target window detection (ignores desktop/taskbar), async invocation
│
├── Input/
│   └── GlobalInputHook.cs      # WH_KEYBOARD_LL + WH_MOUSE_LL (XButton1 / XButton2 / Esc)
│
├── UI/
│   └── TrayIconManager.cs      # System tray NotifyIcon, dynamic icon drawing, context menu
│
└── Views/
    ├── OverlayWindow.xaml/.cs  # Click-through, Layered, Topmost overlay covering VirtualScreen
    └── SettingsWindow.xaml/.cs # Compact Windows 11 Mica settings window with interactive key recorder
```

---

## Getting Started

### Prerequisites

- Windows 10 (version 1903+) or Windows 11 (recommended).
- .NET 8.0 SDK or .NET 9.0 SDK (or run the standalone self-contained build without any runtime installed).

### Building from Source

```powershell
# Clone or navigate to the directory
cd "FocusGlow"

# Build Release version
dotnet build -c Release

# Run the app
dotnet run -c Release
```

### Self-Contained Deployment (Zero Dependencies)

To generate a standalone executable that runs on any PC without requiring .NET to be pre-installed:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

Then run `.\publish\FocusGlow.exe`.

---

## Default Controls

- **Activate / Toggle Focus Mode:** `Alt + F` (or custom hotkey / `Mouse 4` / `Mouse 5`).
- **Exit Focus Mode:** Press `Esc` or press the activation shortcut again.
- **Configure:** Double-click the system tray icon or right-click and choose **Settings**.
