using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;
using FocusGlow.Core;
using FocusGlow.Input;
using FocusGlow.Services;
using FocusGlow.UI;
using FocusGlow.Views;
using Application = System.Windows.Application;

namespace FocusGlow
{
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;
        private GlobalInputHook? _inputHook;
        private FocusAnimationController? _animationController;
        private OverlayWindow? _overlayWindow;
        private FocusManager? _focusManager;
        private SettingsWindow? _settingsWindow;
        private TrayIconManager? _trayIconManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Single-instance mutex check
            _singleInstanceMutex = new Mutex(true, "FocusGlow_SingleInstance_Mutex_W11", out bool isNewInstance);
            if (!isNewInstance)
            {
                System.Windows.MessageBox.Show("FocusGlow уже запущен в системном трее.", "FocusGlow", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 2. Unlock high frame rate (up to 240+ Hz monitors)
            try
            {
                Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(240));
            }
            catch
            {
                // Already overridden or handled
            }

            // 3. Initialize core services
            _animationController = new FocusAnimationController();
            _overlayWindow = new OverlayWindow(_animationController);
            _focusManager = new FocusManager(_animationController, _overlayWindow);

            // 4. Initialize global input hook (keyboard + mouse XButton1/XButton2)
            _inputHook = new GlobalInputHook();
            _inputHook.HotkeyTriggered += () => _focusManager.ToggleFocus();
            _inputHook.EscapePressed += () =>
            {
                if (_focusManager.IsFocusActive)
                {
                    _focusManager.DeactivateFocus();
                }
            };
            _inputHook.Install();

            // 5. Initialize settings window
            _settingsWindow = new SettingsWindow(_focusManager, _inputHook);

            // 6. Initialize system tray icon & menu
            _trayIconManager = new TrayIconManager(_focusManager, OpenSettings);

            // 7. Show settings window or start in tray
            var config = ConfigManager.Current;
            if (!config.StartMinimized)
            {
                OpenSettings();
            }
        }

        public void OpenSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Show();
                if (_settingsWindow.WindowState == WindowState.Minimized)
                {
                    _settingsWindow.WindowState = WindowState.Normal;
                }
                _settingsWindow.Activate();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _inputHook?.Dispose();
            _trayIconManager?.Dispose();
            _overlayWindow?.Dispose();

            if (_settingsWindow != null)
            {
                _settingsWindow.RealClose();
            }

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { }
                _singleInstanceMutex.Dispose();
            }

            base.OnExit(e);
        }
    }
}
