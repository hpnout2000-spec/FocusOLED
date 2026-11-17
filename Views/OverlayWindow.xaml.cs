using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FocusGlow.Core;
using FocusGlow.Interop;
using FocusGlow.Rendering;
using SkiaSharp.Views.Desktop;

namespace FocusGlow.Views
{
    public partial class OverlayWindow : Window, IDisposable
    {
        private readonly FocusAnimationController _controller;
        private readonly FocusOverlayRenderer _renderer;

        private bool _isRendering = false;
        private long _lastTimestamp;

        public OverlayWindow(FocusAnimationController controller)
        {
            InitializeComponent();
            _controller = controller;
            _renderer = new FocusOverlayRenderer();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyWindowExtendedStyles();
        }

        private void ApplyWindowExtendedStyles()
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            int newExStyle = exStyle |
                             Win32.WS_EX_TOPMOST |
                             Win32.WS_EX_TRANSPARENT |
                             Win32.WS_EX_LAYERED |
                             Win32.WS_EX_NOACTIVATE |
                             Win32.WS_EX_TOOLWINDOW;

            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, newExStyle);
        }

        public void ActivateOverlay(IntPtr targetHwnd)
        {
            // Position to cover full virtual screen across all monitors
            int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
            int vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
            int vw = Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN);
            int vh = Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN);

            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.EnsureHandle();
            ApplyWindowExtendedStyles();

            Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, vx, vy, vw, vh,
                Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Show();

            // Start animation controller
            _controller.Activate(targetHwnd);

            // Start 240Hz render loop
            StartRenderLoop();
        }

        public void DeactivateOverlay()
        {
            StopRenderLoop();
            Hide();
        }

        private void StartRenderLoop()
        {
            if (_isRendering) return;

            _lastTimestamp = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += OnCompositionRendering;
            _isRendering = true;
        }

        private void StopRenderLoop()
        {
            if (!_isRendering) return;

            CompositionTarget.Rendering -= OnCompositionRendering;
            _isRendering = false;
        }

        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            long now = Stopwatch.GetTimestamp();
            double dt = (double)(now - _lastTimestamp) / Stopwatch.Frequency;
            _lastTimestamp = now;

            // Guard against large pauses
            if (dt <= 0.0 || dt > 0.1) dt = 1.0 / 60.0;

            _controller.Update(dt);

            // Request Skia repaint on GPU
            SkCanvasElement.InvalidateVisual();
        }

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            _renderer.Render(
                e.Surface.Canvas,
                e.Info.Width,
                e.Info.Height,
                _controller.CurrentTargetRoundRect,
                _controller.CurrentDimOpacity,
                _controller.CurrentGlowAlpha,
                _controller.CurrentGlowProgress
            );
        }

        public void Dispose()
        {
            StopRenderLoop();
            _renderer.Dispose();
        }
    }
}
