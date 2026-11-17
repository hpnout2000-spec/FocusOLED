using System;
using System.Runtime.InteropServices;
using FocusGlow.Interop;
using FocusGlow.Models;
using FocusGlow.Physics;
using FocusGlow.Services;
using SkiaSharp;

namespace FocusGlow.Core
{
    public enum FocusState
    {
        Inactive,
        Activating,
        Active,
        Deactivating
    }

    public sealed class FocusAnimationController
    {
        public FocusState State { get; private set; } = FocusState.Inactive;

        public IntPtr TargetHwnd { get; private set; } = IntPtr.Zero;

        // Current visual coordinates of target window on the canvas
        public SKRoundRect CurrentTargetRoundRect { get; private set; } = new();

        public float CurrentDimOpacity { get; private set; } = 0.0f;
        public float CurrentGlowAlpha { get; private set; } = 0.0f;
        public float CurrentGlowProgress { get; private set; } = 0.0f;

        public bool IsCursorInsideTarget { get; private set; } = true;

        public event Action? DeactivationCompleted;

        // Phase 1 Glow state
        private double _glowTimer = 0.0;
        private double _glowDuration = 1.0;

        // Phase 2 & 3 Dimming transition state
        private float _dimFromOpacity = 0.0f;
        private float _dimTargetOpacity = 0.0f;
        private double _dimTimer = 0.0;
        private double _dimDuration = 0.45;
        private bool _isTwoStageDimming = false;
        private int _currentDimStage = 1; // 1 = 0->50%, 2 = 50->100%

        // Cursor exit transition
        private float _cursorExitFromOpacity = 0.0f;
        private double _cursorExitTimer = 0.0;
        private double _cursorExitDuration = 0.22;

        // Deactivation transition
        private float _deactFromOpacity = 0.0f;
        private double _deactTimer = 0.0;
        private double _deactDuration = 0.22;

        // Smoothing for window rect during resize/drag
        private float _smoothedLeft;
        private float _smoothedTop;
        private float _smoothedRight;
        private float _smoothedBottom;
        private float _smoothedRadius;
        private float _velLeft, _velTop, _velRight, _velBottom, _velRadius;

        public void Activate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd))
                return;

            TargetHwnd = hwnd;
            State = FocusState.Activating;

            var config = ConfigManager.Current;
            _glowDuration = Math.Max(0.1, config.GlowDurationMs / 1000.0);
            _glowTimer = 0.0;
            CurrentGlowAlpha = 0.0f;
            CurrentGlowProgress = 0.0f;

            // Initialize window coordinates
            UpdateWindowCoordinates(instantSnap: true);

            // Phase 2 two-stage dimming initialization
            CurrentDimOpacity = 0.0f;
            StartDimmingSequence(fromOpacity: 0.0f);
        }

        public void Deactivate()
        {
            if (State == FocusState.Inactive || State == FocusState.Deactivating)
                return;

            State = FocusState.Deactivating;
            _deactFromOpacity = CurrentDimOpacity;
            _deactTimer = 0.0;
            _deactDuration = Math.Max(0.05, ConfigManager.Current.CursorExitDurationMs / 1000.0);
        }

        private void StartDimmingSequence(float fromOpacity)
        {
            var config = ConfigManager.Current;
            double speedMult = Math.Max(0.1, config.DimSpeedMultiplier);
            double baseFast = (config.FastDimDurationMs / 1000.0) / speedMult;
            double baseDeep = (config.DeepDimDurationMs / 1000.0) / speedMult;

            _dimFromOpacity = fromOpacity;
            _dimTimer = 0.0;
            _isTwoStageDimming = true;

            if (fromOpacity < 0.5f)
            {
                // Stage 1: from current to 0.5
                _currentDimStage = 1;
                _dimTargetOpacity = 0.5f;
                // Proportional duration for remaining fraction
                float fraction = (0.5f - fromOpacity) / 0.5f;
                _dimDuration = Math.Max(0.05, baseFast * fraction);
            }
            else
            {
                // Stage 2: from current to 1.0
                _currentDimStage = 2;
                _dimTargetOpacity = 1.0f;
                float fraction = (1.0f - fromOpacity) / 0.5f;
                _dimDuration = Math.Max(0.05, baseDeep * fraction);
            }
        }

        public void Update(double dt)
        {
            if (State == FocusState.Inactive)
                return;

            // 1. Verify target window validity
            if (!Win32.IsWindow(TargetHwnd) || !Win32.IsWindowVisible(TargetHwnd))
            {
                Deactivate();
            }

            // 2. Real-time window bounds tracking
            UpdateWindowCoordinates(instantSnap: false);

            // 3. Phase 1: Glow comet update
            UpdateGlowPhase(dt);

            // 4. Cursor tracking & Phase 3 reaction
            UpdateCursorReaction(dt);

            // 5. Check deactivation completion
            if (State == FocusState.Deactivating)
            {
                _deactTimer += dt;
                float t = Math.Clamp((float)(_deactTimer / _deactDuration), 0f, 1f);
                float eased = CubicBezier.AppleDecel.Evaluate(t);
                CurrentDimOpacity = Math.Clamp(_deactFromOpacity * (1.0f - eased), 0f, 1f);

                // Fade out any remaining glow
                CurrentGlowAlpha = Math.Clamp(CurrentGlowAlpha * (1.0f - eased), 0f, 1f);

                if (t >= 1.0f && CurrentDimOpacity <= 0.001f && CurrentGlowAlpha <= 0.001f)
                {
                    CurrentDimOpacity = 0.0f;
                    CurrentGlowAlpha = 0.0f;
                    State = FocusState.Inactive;
                    TargetHwnd = IntPtr.Zero;
                    DeactivationCompleted?.Invoke();
                }
            }
        }

        private void UpdateGlowPhase(double dt)
        {
            if (_glowTimer < _glowDuration)
            {
                _glowTimer += dt;
                float p = Math.Clamp((float)(_glowTimer / _glowDuration), 0f, 1f);
                CurrentGlowProgress = p;

                // Alpha envelope:
                // 0.00 - 0.15: Fade in (0 -> 1)
                // 0.15 - 0.65: Full brightness (1.0)
                // 0.65 - 1.00: Fade out / dissolve (1 -> 0)
                if (p < 0.15f)
                {
                    CurrentGlowAlpha = p / 0.15f;
                }
                else if (p <= 0.65f)
                {
                    CurrentGlowAlpha = 1.0f;
                }
                else
                {
                    float dissolveT = (p - 0.65f) / 0.35f;
                    CurrentGlowAlpha = 1.0f - CubicBezier.AppleDecel.Evaluate(dissolveT);
                }
            }
            else
            {
                CurrentGlowAlpha = 0.0f;
                if (State == FocusState.Activating)
                {
                    State = FocusState.Active;
                }
            }
        }

        private void UpdateCursorReaction(double dt)
        {
            if (State == FocusState.Deactivating)
                return;

            // Get physical cursor position
            Win32.GetCursorPos(out Win32.POINT cursorPt);

            // Get target window physical bounds
            int hr = Win32.DwmGetWindowAttribute(TargetHwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT winRect, Marshal.SizeOf<Win32.RECT>());
            bool inside = hr == 0 && winRect.Contains(cursorPt.X, cursorPt.Y);

            var config = ConfigManager.Current;
            double exitDuration = Math.Max(0.05, config.CursorExitDurationMs / 1000.0);

            if (inside != IsCursorInsideTarget)
            {
                // Cursor transitioned between inside and outside
                IsCursorInsideTarget = inside;

                if (!inside)
                {
                    // Cursor LEFT target window: smoothly fade dimming down to 0%
                    _cursorExitFromOpacity = CurrentDimOpacity;
                    _cursorExitTimer = 0.0;
                    _cursorExitDuration = exitDuration;
                    _isTwoStageDimming = false;
                }
                else
                {
                    // Cursor RETURNED to target window:
                    // Resume two-stage dimming from current intermediate opacity towards 50% then 100%
                    StartDimmingSequence(fromOpacity: CurrentDimOpacity);
                }
            }

            if (!IsCursorInsideTarget)
            {
                // Smooth deceleration down to 0% (~220 ms)
                _cursorExitTimer += dt;
                float t = Math.Clamp((float)(_cursorExitTimer / _cursorExitDuration), 0f, 1f);
                float eased = CubicBezier.AppleDecel.Evaluate(t);
                CurrentDimOpacity = Math.Clamp(_cursorExitFromOpacity * (1.0f - eased), 0f, 1f);
            }
            else if (_isTwoStageDimming)
            {
                // Two-stage dimming sequence
                _dimTimer += dt;
                float t = Math.Clamp((float)(_dimTimer / _dimDuration), 0f, 1f);
                float eased = CubicBezier.AppleDecel.Evaluate(t);

                CurrentDimOpacity = Math.Clamp(_dimFromOpacity + (_dimTargetOpacity - _dimFromOpacity) * eased, 0f, 1f);

                if (t >= 1.0f)
                {
                    if (_currentDimStage == 1)
                    {
                        // Transition to Stage 2: 50% -> 100% over ~1.5s
                        _currentDimStage = 2;
                        _dimFromOpacity = 0.5f;
                        _dimTargetOpacity = 1.0f;
                        _dimTimer = 0.0;
                        double speedMult = Math.Max(0.1, config.DimSpeedMultiplier);
                        _dimDuration = Math.Max(0.1, (config.DeepDimDurationMs / 1000.0) / speedMult);
                    }
                    else
                    {
                        // Succeeded 100% deep dimming
                        CurrentDimOpacity = 1.0f;
                        _isTwoStageDimming = false;
                    }
                }
            }
        }

        private void UpdateWindowCoordinates(bool instantSnap)
        {
            if (TargetHwnd == IntPtr.Zero) return;

            int hr = Win32.DwmGetWindowAttribute(TargetHwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT winRect, Marshal.SizeOf<Win32.RECT>());
            if (hr != 0) return;

            // Virtual screen offset in physical pixels
            int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
            int vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);

            float localLeft = winRect.Left - vx;
            float localTop = winRect.Top - vy;
            float localRight = winRect.Right - vx;
            float localBottom = winRect.Bottom - vy;

            // Windows 11 rounded corners logic:
            // Maximized window -> 0px radius. Normal window -> 8px base (scaled by DPI).
            bool isZoomed = Win32.IsZoomed(TargetHwnd);
            float targetRadius = 0f;
            if (!isZoomed)
            {
                uint dpi = Win32.GetDpiForWindow(TargetHwnd);
                if (dpi == 0) dpi = 96;
                float dpiScale = dpi / 96f;
                targetRadius = ConfigManager.Current.CornerRadius * dpiScale;
            }

            if (instantSnap)
            {
                _smoothedLeft = localLeft;
                _smoothedTop = localTop;
                _smoothedRight = localRight;
                _smoothedBottom = localBottom;
                _smoothedRadius = targetRadius;
                _velLeft = _velTop = _velRight = _velBottom = _velRadius = 0f;
            }
            else
            {
                // Snappy 40ms damp for silky tracking during window drag/resize
                const float smoothTime = 0.035f;
                const float maxSpeed = 8000f;
                float dt = 0.016f;

                _smoothedLeft = SmoothDamp.Update(_smoothedLeft, localLeft, ref _velLeft, smoothTime, maxSpeed, dt);
                _smoothedTop = SmoothDamp.Update(_smoothedTop, localTop, ref _velTop, smoothTime, maxSpeed, dt);
                _smoothedRight = SmoothDamp.Update(_smoothedRight, localRight, ref _velRight, smoothTime, maxSpeed, dt);
                _smoothedBottom = SmoothDamp.Update(_smoothedBottom, localBottom, ref _velBottom, smoothTime, maxSpeed, dt);
                _smoothedRadius = SmoothDamp.Update(_smoothedRadius, targetRadius, ref _velRadius, smoothTime, maxSpeed, dt);
            }

            var skRect = new SKRect(_smoothedLeft, _smoothedTop, _smoothedRight, _smoothedBottom);
            CurrentTargetRoundRect = new SKRoundRect(skRect, _smoothedRadius, _smoothedRadius);
        }
    }
}
