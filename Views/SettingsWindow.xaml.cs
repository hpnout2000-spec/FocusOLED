using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FocusGlow.Core;
using FocusGlow.Input;
using FocusGlow.Models;
using FocusGlow.Services;
using Color = System.Windows.Media.Color;
using FocusManager = FocusGlow.Core.FocusManager;

namespace FocusGlow.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly FocusManager _focusManager;
        private readonly GlobalInputHook _inputHook;
        private bool _isRealClose = false;

        public SettingsWindow(FocusManager focusManager, GlobalInputHook inputHook)
        {
            InitializeComponent();
            _focusManager = focusManager;
            _inputHook = inputHook;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadConfigToUi();
            ConfigManager.ConfigChanged += OnConfigChanged;
        }

        private void LoadConfigToUi()
        {
            var config = ConfigManager.Current;

            HotkeyDisplayText.Text = config.Hotkey.DisplayText;
            CornerRadiusSlider.Value = config.CornerRadius;
            CornerRadiusValueText.Text = $"{(int)config.CornerRadius} px";

            DimSpeedSlider.Value = config.DimSpeedMultiplier;
            DimSpeedValueText.Text = $"{config.DimSpeedMultiplier:0.0}x";
        }

        private void OnConfigChanged(AppConfig config)
        {
            Dispatcher.Invoke(() =>
            {
                HotkeyDisplayText.Text = config.Hotkey.DisplayText;
            });
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

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

            // Enter recording mode
            RecordHotkeyButton.Content = "Отмена";
            HotkeyDisplayText.Text = "Нажмите клавишу или XButton...";
            HotkeyBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0xAF, 0x5F)); // Amber

            _inputHook.StartRecording(newBinding =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConfigManager.Update(cfg =>
                    {
                        cfg.Hotkey = newBinding;
                    });

                    ResetRecordUi();
                });
            });
        }

        private void ResetRecordUi()
        {
            RecordHotkeyButton.Content = "Записать";
            HotkeyBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x85, 0xF4)); // Blue
            HotkeyDisplayText.Text = ConfigManager.Current.Hotkey.DisplayText;
        }

        private void CornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CornerRadiusValueText == null) return;
            float radius = (float)e.NewValue;
            CornerRadiusValueText.Text = $"{(int)radius} px";

            ConfigManager.Update(cfg =>
            {
                cfg.CornerRadius = radius;
            });
        }

        private void DimSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DimSpeedValueText == null) return;
            double speed = Math.Round(e.NewValue, 1);
            DimSpeedValueText.Text = $"{speed:0.0}x";

            ConfigManager.Update(cfg =>
            {
                cfg.DimSpeedMultiplier = speed;
            });
        }

        private void TestFocusButton_Click(object sender, RoutedEventArgs e)
        {
            // Minimize or toggle focus
            _focusManager.ToggleFocus();
        }
    }
}
