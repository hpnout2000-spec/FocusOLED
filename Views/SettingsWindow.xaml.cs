using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FocusGlow.Input;
using FocusGlow.Interop;
using FocusGlow.Models;
using FocusGlow.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FocusManager = FocusGlow.Core.FocusManager;

namespace FocusGlow.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly GlobalInputHook _inputHook;
        private bool _isRealClose;

        private static readonly Brush DefaultPillBorder = new SolidColorBrush(Color.FromRgb(0x34, 0x34, 0x38));
        private static readonly Brush RecordingPillBorder = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));

        public SettingsWindow(FocusManager focusManager, GlobalInputHook inputHook)
        {
            InitializeComponent();
            _inputHook = inputHook;
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int darkMode = 1;
            Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Включаем нативный материал Windows 11 Mica; при неудаче остаётся чистый #161617
            int micaBackdrop = 2;
            if (Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_SYSTEMBACKDROP_TYPE, ref micaBackdrop, sizeof(int)) == 0)
            {
                Background = System.Windows.Media.Brushes.Transparent;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var config = ConfigManager.Current;
            HotkeyPillButton.Content = config.Hotkey.DisplayText;
            CornerRadiusSlider.Value = config.CornerRadius;
            CornerRadiusValueText.Text = $"{(int)config.CornerRadius} px";
            DimSpeedSlider.Value = config.DimSpeedMultiplier;
            DimSpeedValueText.Text = $"{config.DimSpeedMultiplier:0.0}×";

            ConfigManager.ConfigChanged += OnConfigChanged;
        }

        private void OnConfigChanged(AppConfig config)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_inputHook.IsRecording)
                    HotkeyPillButton.Content = config.Hotkey.DisplayText;
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isRealClose)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        public void RealClose()
        {
            _isRealClose = true;
            Close();
        }

        private void RecordHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_inputHook.IsRecording)
            {
                _inputHook.CancelRecording();
                ResetRecordUi();
                return;
            }

            HotkeyPillButton.Content = "Press key...";
            HotkeyPillButton.BorderBrush = RecordingPillBorder;

            _inputHook.StartRecording(newBinding =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConfigManager.Update(cfg => cfg.Hotkey = newBinding);
                    ResetRecordUi();
                });
            });
        }

        private void ResetRecordUi()
        {
            HotkeyPillButton.BorderBrush = DefaultPillBorder;
            HotkeyPillButton.Content = ConfigManager.Current.Hotkey.DisplayText;
        }

        private void CornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CornerRadiusValueText == null) return;
            float radius = (float)e.NewValue;
            CornerRadiusValueText.Text = $"{(int)radius} px";
            ConfigManager.Update(cfg => cfg.CornerRadius = radius);
        }

        private void DimSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DimSpeedValueText == null) return;
            double speed = Math.Round(e.NewValue, 1);
            DimSpeedValueText.Text = $"{speed:0.0}×";
            ConfigManager.Update(cfg => cfg.DimSpeedMultiplier = speed);
        }
    }
}
