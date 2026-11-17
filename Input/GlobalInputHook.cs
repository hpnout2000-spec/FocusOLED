using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FocusGlow.Interop;
using FocusGlow.Models;
using FocusGlow.Services;

namespace FocusGlow.Input
{
    public sealed class GlobalInputHook : IDisposable
    {
        private Win32.HookProc? _keyboardProc;
        private Win32.HookProc? _mouseProc;

        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;

        public event Action? HotkeyTriggered;
        public event Action? EscapePressed;

        public bool IsRecording { get; private set; }
        private Action<HotkeyBinding>? _onHotkeyRecorded;

        public bool IsActive => _keyboardHookId != IntPtr.Zero;

        public void Install()
        {
            if (IsActive) return;

            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr moduleHandle = Win32.GetModuleHandle(curModule?.ModuleName);

            _keyboardHookId = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            _mouseHookId = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        }

        public void Uninstall()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }

            if (_mouseHookId != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }
        }

        public void StartRecording(Action<HotkeyBinding> onRecorded)
        {
            _onHotkeyRecorded = onRecorded;
            IsRecording = true;
        }

        public void CancelRecording()
        {
            IsRecording = false;
            _onHotkeyRecorded = null;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
                {
                    var kbd = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                    var key = (Keys)kbd.vkCode;

                    // Check for Escape to cancel recording or deactivate focus
                    if (key == Keys.Escape)
                    {
                        if (IsRecording)
                        {
                            CancelRecording();
                            return (IntPtr)1; // swallow escape
                        }

                        EscapePressed?.Invoke();
                    }
                    else
                    {
                        var modifiers = GetCurrentModifiers();

                        if (IsRecording)
                        {
                            // Skip standalone modifier key presses during recording
                            if (key is not (Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
                                            Keys.Menu or Keys.LMenu or Keys.RMenu or
                                            Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
                                            Keys.LWin or Keys.RWin))
                            {
                                var binding = new HotkeyBinding
                                {
                                    Modifiers = modifiers,
                                    Key = key.ToString(),
                                    MouseButton = MouseSideButton.None
                                };

                                IsRecording = false;
                                var cb = _onHotkeyRecorded;
                                _onHotkeyRecorded = null;
                                cb?.Invoke(binding);

                                return (IntPtr)1; // swallow recorded key
                            }
                        }
                        else
                        {
                            // Match configured hotkey
                            var binding = ConfigManager.Current.Hotkey;
                            if (!binding.IsMouseButton &&
                                string.Equals(binding.Key, key.ToString(), StringComparison.OrdinalIgnoreCase) &&
                                binding.Modifiers == modifiers)
                            {
                                HotkeyTriggered?.Invoke();
                                return (IntPtr)1; // swallow hotkey
                            }
                        }
                    }
                }
            }

            return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == Win32.WM_XBUTTONDOWN)
                {
                    var ms = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    int xButton = (int)((ms.mouseData >> 16) & 0xFFFF);

                    var sideButton = xButton switch
                    {
                        Win32.XBUTTON1 => MouseSideButton.XButton1,
                        Win32.XBUTTON2 => MouseSideButton.XButton2,
                        _ => MouseSideButton.None
                    };

                    if (sideButton != MouseSideButton.None)
                    {
                        var modifiers = GetCurrentModifiers();

                        if (IsRecording)
                        {
                            var binding = new HotkeyBinding
                            {
                                Modifiers = modifiers,
                                Key = string.Empty,
                                MouseButton = sideButton
                            };

                            IsRecording = false;
                            var cb = _onHotkeyRecorded;
                            _onHotkeyRecorded = null;
                            cb?.Invoke(binding);

                            return (IntPtr)1; // swallow side button
                        }

                        var currentBinding = ConfigManager.Current.Hotkey;
                        if (currentBinding.IsMouseButton &&
                            currentBinding.MouseButton == sideButton &&
                            currentBinding.Modifiers == modifiers)
                        {
                            HotkeyTriggered?.Invoke();
                            return (IntPtr)1; // swallow side button
                        }
                    }
                }
            }

            return Win32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private static HotkeyModifiers GetCurrentModifiers()
        {
            var modifiers = HotkeyModifiers.None;

            if ((Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0)
                modifiers |= HotkeyModifiers.Control;

            if ((Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0)
                modifiers |= HotkeyModifiers.Alt;

            if ((Win32.GetAsyncKeyState(Win32.VK_SHIFT) & 0x8000) != 0)
                modifiers |= HotkeyModifiers.Shift;

            if ((Win32.GetAsyncKeyState(Win32.VK_LWIN) & 0x8000) != 0 ||
                (Win32.GetAsyncKeyState(Win32.VK_RWIN) & 0x8000) != 0)
                modifiers |= HotkeyModifiers.Windows;

            return modifiers;
        }

        public void Dispose()
        {
            Uninstall();
        }
    }
}
