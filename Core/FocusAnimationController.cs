using System;
using System.Runtime.InteropServices;
using FocusGlow.Interop;
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
        public SKRoundRect CurrentTargetRoundRect { get; private set; } = new();

        public float CurrentDimOpacity { get; private set; }
        public float CurrentGlowAlpha { get; private set; }
        public float CurrentGlowProgress { get; private set; }
        public bool IsCursorInsideTarget { get; private set; } = true;

        public event Action? DeactivationCompleted;

        private double _glowTimer;
        private double _glowDuration = 0.85;

        private float _dimFromOpacity;
        private float _dimTargetOpacity;
        private double _dimTimer;
        private double _dimDuration = 0.45;
        private bool _isTwoStageDimming;
        private int _currentDimStage = 1;

        private float _cursorExitFromOpacity;
        private double _cursorExitTimer;
        private double _cursorExitDuration = 0.22;

        private float _deactFromOpacity;
        private double _deactTimer;
        private double _deactDuration = 0.20;

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
            IsCursorInsideTarget = true;

            var config = ConfigManager.Current;
            _glowDuration = Math.Clamp(config.GlowDurationMs / 1000.0, 0.4, 1.5);
            if (Math.Abs(config.GlowDurationMs - 1000.0) < 1.0)
                _glowDuration = 0.85; // более собранный, быстрый импульс по умолчанию

            _glowTimer = 0.0;
            CurrentGlowAlpha = 0.0f;
            CurrentGlowProgress = 0.0f;

            UpdateWindowCoordinates(instantSnap: true, dt: 0.016);

            CurrentDimOpacity = 0.0f;
            StartDimmingSequence(fromOpacity: 0.0f);
        }

        public void Deactivate()
        {
            if (State is FocusState.Inactive or FocusState.Deactivating)
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
                _currentDimStage = 1;
                _dimTargetOpacity = 0.5f;
                float fraction = (0.5f - fromOpacity) / 0.5f;
                _dimDuration = Math.Max(0.05, baseFast * fraction);
            }
            else
            {
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

            if (!Win32.IsWindow(TargetHwnd) || !Win32.IsWindowVisible(TargetHwnd) || Win32.IsIconic(TargetHwnd))
            {
                Deactivate();
            }

            UpdateWindowCoordinates(instantSnap: false, dt: dt);
            UpdateGlowPhase(dt);
            UpdateCursorReaction(dt);

            if (State == FocusState.Deactivating)
            {
                _deactTimer += dt;
                float t = Math.Clamp((float)(_deactTimer / _deactDuration), 0f, 1f);
                float eased = CubicBezier.AppleDecel.Evaluate(t);
                CurrentDimOpacity = Math.Clamp(_deactFromOpacity * (1.0f - eased), 0f, 1f);
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

                // Мягкое появление за 12%, удержание до 52%, затем полное растворение к концу первой фазы затемнения
                if (p < 0.12f)
                {
                    CurrentGlowAlpha = CubicBezier.EaseOut.Evaluate(p / 0.12f);
                }
                else if (p <= 0.52f)
                {
                    CurrentGlowAlpha = 1.0f;
                }
                else
                {
                    float dissolveT = (p - 0.52f) / 0.48f;
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

            Win32.GetCursorPos(out Win32.POINT cursorPt);

            int hr = Win32.DwmGetWindowAttribute(TargetHwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT winRect, Marshal.SizeOf<Win32.RECT>());
            bool inside = hr == 0 && winRect.Contains(cursorPt.X, cursorPt.Y);

            var config = ConfigManager.Current;
            double exitDuration = Math.Max(0.05, config.CursorExitDurationMs / 1000.0);

            if (inside != IsCursorInsideTarget)
            {
                IsCursorInsideTarget = inside;

                if (!inside)
                {
                    _cursorExitFromOpacity = CurrentDimOpacity;
                    _cursorExitTimer = 0.0;
                    _cursorExitDuration = exitDuration;
                    _isTwoStageDimming = false;
                }
                else
                {
                    StartDimmingSequence(fromOpacity: CurrentDimOpacity);
                }
            }

            if (!IsCursorInsideTarget)
            {
                _cursorExitTimer += dt;
                float t = Math.Clamp((float)(_cursorExitTimer / _cursorExitDuration), 0f, 1f);
                float eased = CubicBezier.AppleDecel.Evaluate(t);
                CurrentDimOpacity = Math.Clamp(_cursorExitFromOpacity * (1.0f - eased), 0f, 1f);
            }
            else if (_isTwoStageDimming)
            {
                _dimTimer += dt;
                float t = Math.Clamp((float)(_dimTimer / _dimDuration), 0f, 1f);
                float eased = CubicBezier.AppleDecel.Evaluate(t);

                CurrentDimOpacity = Math.Clamp(_dimFromOpacity + (_dimTargetOpacity - _dimFromOpacity) * eased, 0f, 1f);

                if (t >= 1.0f)
                {
                    if (_currentDimStage == 1)
                    {
                        _currentDimStage = 2;
                        _dimFromOpacity = 0.5f;
                        _dimTargetOpacity = 1.0f;
                        _dimTimer = 0.0;
                        double speedMult = Math.Max(0.1, config.DimSpeedMultiplier);
                        _dimDuration = Math.Max(0.1, (config.DeepDimDurationMs / 1000.0) / speedMult);
                    }
                    else
                    {
                        CurrentDimOpacity = 1.0f;
                        _isTwoStageDimming = false;
                    }
                }
            }
        }

        private void UpdateWindowCoordinates(bool instantSnap, double dt)
        {
            if (TargetHwnd == IntPtr.Zero) return;

            int hr = Win32.DwmGetWindowAttribute(TargetHwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT winRect, Marshal.SizeOf<Win32.RECT>());
            if (hr != 0) return;

            int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
            int vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);

            float localLeft = winRect.Left - vx;
            float localTop = winRect.Top - vy;
            float localRight = winRect.Right - vx;
            float localBottom = winRect.Bottom - vy;

            bool isZoomed = Win32.IsZoomed(TargetHwnd);
            float targetRadius = 0f;
            if (!isZoomed)
            {
                // Компенсация 1px DWM-асимметрии Windows 11:
                // DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) на Win11
                // захватывает лишний 1px с левого, верхнего и нижнего краёв.
                localLeft += 1f;
                localTop += 1f;
                localBottom -= 1f;

                uint dpi = Win32.GetDpiForWindow(TargetHwnd);
                if (dpi == 0) dpi = 96;
                targetRadius = ConfigManager.Current.CornerRadius * (dpi / 96f);
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
                const float smoothTime = 0.025f;
                const float maxSpeed = 12000f;
                float fdt = (float)Math.Clamp(dt, 0.001, 0.05);

                _smoothedLeft = SmoothDamp.Update(_smoothedLeft, localLeft, ref _velLeft, smoothTime, maxSpeed, fdt);
                _smoothedTop = SmoothDamp.Update(_smoothedTop, localTop, ref _velTop, smoothTime, maxSpeed, fdt);
                _smoothedRight = SmoothDamp.Update(_smoothedRight, localRight, ref _velRight, smoothTime, maxSpeed, fdt);
                _smoothedBottom = SmoothDamp.Update(_smoothedBottom, localBottom, ref _velBottom, smoothTime, maxSpeed, fdt);
                _smoothedRadius = SmoothDamp.Update(_smoothedRadius, targetRadius, ref _velRadius, smoothTime, maxSpeed, fdt);

                // Мгновенная фиксация к целым пикселям в покое для исключения субпиксельного размытия сглаживания
                if (Math.Abs(_smoothedLeft - localLeft) < 0.25f) _smoothedLeft = localLeft;
                if (Math.Abs(_smoothedTop - localTop) < 0.25f) _smoothedTop = localTop;
                if (Math.Abs(_smoothedRight - localRight) < 0.25f) _smoothedRight = localRight;
                if (Math.Abs(_smoothedBottom - localBottom) < 0.25f) _smoothedBottom = localBottom;
                if (Math.Abs(_smoothedRadius - targetRadius) < 0.1f) _smoothedRadius = targetRadius;
            }

            CurrentTargetRoundRect = new SKRoundRect(
                new SKRect(_smoothedLeft, _smoothedTop, _smoothedRight, _smoothedBottom),
                _smoothedRadius,
                _smoothedRadius);
        }
    }
}
