using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FocusGlow.Core;
using FocusGlow.Models;
using FocusGlow.Services;

namespace FocusGlow.UI
{
    public sealed class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly FocusManager _focusManager;
        private readonly Action _openSettingsAction;

        private ToolStripMenuItem? _toggleMenuItem;

        public TrayIconManager(FocusManager focusManager, Action openSettingsAction)
        {
            _focusManager = focusManager;
            _openSettingsAction = openSettingsAction;

            _notifyIcon = new NotifyIcon
            {
                Text = "FocusGlow",
                Visible = true,
                Icon = CreateTrayIcon()
            };

            _notifyIcon.DoubleClick += (_, _) => _openSettingsAction();

            BuildContextMenu();

            _focusManager.FocusStateChanged += OnFocusStateChanged;
            ConfigManager.ConfigChanged += OnConfigChanged;
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var settingsItem = new ToolStripMenuItem("Settings", null, (_, _) => _openSettingsAction());

            string hotkeyText = ConfigManager.Current.Hotkey.DisplayText;
            _toggleMenuItem = new ToolStripMenuItem($"Enable Focus ({hotkeyText})", null, (_, _) =>
            {
                _focusManager.ToggleFocus();
            });

            var exitItem = new ToolStripMenuItem("Exit", null, (_, _) =>
            {
                System.Windows.Application.Current.Shutdown();
            });

            menu.Items.Add(settingsItem);
            menu.Items.Add(_toggleMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void OnConfigChanged(AppConfig config)
        {
            if (_toggleMenuItem != null)
            {
                _toggleMenuItem.Text = _focusManager.IsFocusActive
                    ? $"Disable Focus ({config.Hotkey.DisplayText})"
                    : $"Enable Focus ({config.Hotkey.DisplayText})";
            }
        }

        private void OnFocusStateChanged(bool active)
        {
            if (_toggleMenuItem != null)
            {
                string hotkeyText = ConfigManager.Current.Hotkey.DisplayText;
                _toggleMenuItem.Text = active
                    ? $"Disable Focus ({hotkeyText})"
                    : $"Enable Focus ({hotkeyText})";
            }
        }

        private static Icon CreateTrayIcon()
        {
            using var bitmap = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var bgBrush = new SolidBrush(Color.FromArgb(24, 24, 26));
            g.FillEllipse(bgBrush, 2, 2, 28, 28);

            using var ringPen = new Pen(Color.FromArgb(235, 235, 240), 2.2f);
            g.DrawEllipse(ringPen, 7, 7, 18, 18);

            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
