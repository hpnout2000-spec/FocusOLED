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
                Text = "FocusGlow — Режим фокуса Windows 11",
                Visible = true,
                Icon = CreateTrayIcon()
            };

            _notifyIcon.DoubleClick += (s, e) => _openSettingsAction();

            BuildContextMenu();

            _focusManager.FocusStateChanged += OnFocusStateChanged;
            ConfigManager.ConfigChanged += OnConfigChanged;
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var settingsItem = new ToolStripMenuItem("⚙️ Настройки", null, (s, e) => _openSettingsAction())
            {
                Font = new Font(menu.Font, FontStyle.Bold)
            };

            string hotkeyText = ConfigManager.Current.Hotkey.DisplayText;
            _toggleMenuItem = new ToolStripMenuItem($"✨ Переключить фокус ({hotkeyText})", null, (s, e) =>
            {
                _focusManager.ToggleFocus();
            });

            var exitItem = new ToolStripMenuItem("❌ Выход", null, (s, e) =>
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
                _toggleMenuItem.Text = $"✨ Переключить фокус ({config.Hotkey.DisplayText})";
            }
        }

        private void OnFocusStateChanged(bool active)
        {
            if (_toggleMenuItem != null)
            {
                string hotkeyText = ConfigManager.Current.Hotkey.DisplayText;
                _toggleMenuItem.Text = active
                    ? $"⏹️ Выключить фокус ({hotkeyText})"
                    : $"✨ Включить фокус ({hotkeyText})";
            }
        }

        private static Icon CreateTrayIcon()
        {
            // Generate a vibrant glowing ring icon matching Apple Intelligence / Gemini colors
            using var bitmap = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Outer dark badge
            using var bgBrush = new SolidBrush(Color.FromArgb(28, 28, 30));
            g.FillEllipse(bgBrush, 2, 2, 28, 28);

            // Radiant gradient ring
            using var path = new GraphicsPath();
            path.AddEllipse(5, 5, 22, 22);

            using var pen = new Pen(Color.FromArgb(66, 133, 244), 3.5f);
            g.DrawPath(pen, path);

            // Gemini magenta/coral accent dot in the center
            using var dotBrush = new SolidBrush(Color.FromArgb(217, 101, 112));
            g.FillEllipse(dotBrush, 12, 12, 8, 8);

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
