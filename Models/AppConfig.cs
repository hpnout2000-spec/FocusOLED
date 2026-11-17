using System;
using System.Text.Json.Serialization;

namespace FocusGlow.Models
{
    [Flags]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MouseSideButton
    {
        None = 0,
        XButton1 = 1, // Back button
        XButton2 = 2  // Forward button
    }

    public sealed class HotkeyBinding
    {
        public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Alt;
        public string Key { get; set; } = "F";
        public MouseSideButton MouseButton { get; set; } = MouseSideButton.None;

        public bool IsMouseButton => MouseButton != MouseSideButton.None;

        public string DisplayText
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
                if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
                if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
                if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");

                if (IsMouseButton)
                {
                    parts.Add(MouseButton switch
                    {
                        MouseSideButton.XButton1 => "Mouse 4 (XButton1)",
                        MouseSideButton.XButton2 => "Mouse 5 (XButton2)",
                        _ => MouseButton.ToString()
                    });
                }
                else
                {
                    parts.Add(string.IsNullOrWhiteSpace(Key) ? "None" : Key);
                }

                return string.Join(" + ", parts);
            }
        }
    }

    public sealed class AppConfig
    {
        public HotkeyBinding Hotkey { get; set; } = new HotkeyBinding();

        /// <summary>
        /// Base corner radius for normal windows in DIP (Win11 default is 8).
        /// </summary>
        public float CornerRadius { get; set; } = 8f;

        /// <summary>
        /// Duration of Phase 1 glow comet in milliseconds.
        /// </summary>
        public double GlowDurationMs { get; set; } = 1000.0;

        /// <summary>
        /// Duration of Stage 2A fast dimming (0% to 50%) in milliseconds.
        /// </summary>
        public double FastDimDurationMs { get; set; } = 450.0;

        /// <summary>
        /// Duration of Stage 2B slow dimming (50% to 100%) in milliseconds.
        /// </summary>
        public double DeepDimDurationMs { get; set; } = 1500.0;

        /// <summary>
        /// Duration of cursor exit dimming fadeout (current to 0%) in milliseconds.
        /// </summary>
        public double CursorExitDurationMs { get; set; } = 220.0;

        /// <summary>
        /// Multiplier for overall dimming speed (default 1.0).
        /// </summary>
        public double DimSpeedMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Whether closing or minimizing the settings window hides it to system tray.
        /// </summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>
        /// Start minimized to system tray.
        /// </summary>
        public bool StartMinimized { get; set; } = false;
    }
}
