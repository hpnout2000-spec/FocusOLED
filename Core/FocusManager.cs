using System;
using FocusGlow.Interop;
using FocusGlow.Views;
using Application = System.Windows.Application;

namespace FocusGlow.Core
{
    public sealed class FocusManager
    {
        private readonly FocusAnimationController _controller;
        private readonly OverlayWindow _overlayWindow;

        public bool IsFocusActive => _controller.State is FocusState.Activating or FocusState.Active;

        public event Action<bool>? FocusStateChanged;

        public FocusManager(FocusAnimationController controller, OverlayWindow overlayWindow)
        {
            _controller = controller;
            _overlayWindow = overlayWindow;
            _controller.DeactivationCompleted += OnDeactivationCompleted;
        }

        public void ToggleFocus()
        {
            if (IsFocusActive)
                DeactivateFocus();
            else
                ActivateFocusUnderCursor();
        }

        public void ActivateFocusUnderCursor()
        {
            IntPtr targetHwnd = FindTopLevelWindowUnderCursor();
            if (targetHwnd == IntPtr.Zero)
                return;

            ActivateFocus(targetHwnd);
        }

        public void ActivateFocus(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !Win32.IsWindow(targetHwnd))
                return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _overlayWindow.ActivateOverlay(targetHwnd);
                FocusStateChanged?.Invoke(true);
            });
        }

        public void DeactivateFocus()
        {
            if (!IsFocusActive) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _controller.Deactivate();
            });
        }

        private void OnDeactivationCompleted()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _overlayWindow.DeactivateOverlay();
                FocusStateChanged?.Invoke(false);
            });
        }

        public static IntPtr FindTopLevelWindowUnderCursor()
        {
            if (!Win32.GetCursorPos(out Win32.POINT cursorPt))
                return IntPtr.Zero;

            IntPtr pointHwnd = Win32.WindowFromPoint(cursorPt);
            if (pointHwnd == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr rootHwnd = Win32.GetAncestor(pointHwnd, Win32.GA_ROOT);
            if (rootHwnd == IntPtr.Zero)
                rootHwnd = pointHwnd;

            if (!Win32.IsWindowVisible(rootHwnd) || Win32.IsDesktopOrTaskbar(rootHwnd))
                return IntPtr.Zero;

            return rootHwnd;
        }
    }
}
