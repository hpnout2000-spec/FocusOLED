using System;
using FocusGlow.Physics;
using SkiaSharp;

namespace FocusGlow.Rendering
{
    public sealed class FocusOverlayRenderer : IDisposable
    {
        // Направленный оптический блик: 65% периметра остаются тёмными,
        // ядро — чистый жемчужно-белый свет, по краям — деликатная хроматическая дисперсия.
        private static readonly SKColor[] OpticalColors =
        {
            new(255, 255, 255, 0),
            new(255, 255, 255, 0),
            new(168, 199, 250, 48),   // Лёгкий ледяной оттенок в хвосте
            new(203, 184, 248, 135),  // Мягкая лавандовая дисперсия
            new(255, 255, 255, 255),  // Чистое белое ядро блика
            new(245, 224, 198, 165),  // Тёплый перламутровый фронт
            new(168, 199, 250, 40),   // Мягкое затухание впереди
            new(255, 255, 255, 0),
            new(255, 255, 255, 0)
        };

        private static readonly float[] OpticalPositions =
        {
            0.00f,
            0.22f,
            0.31f,
            0.39f,
            0.46f,
            0.52f,
            0.58f,
            0.66f,
            1.00f
        };

        private readonly SKPaint _dimPaint;
        private readonly SKPaint _ambientHaloPaint;
        private readonly SKPaint _opticalBloomPaint;
        private readonly SKPaint _specularHairlinePaint;

        private readonly SKMaskFilter _ambientBlurFilter;
        private readonly SKMaskFilter _bloomBlurFilter;
        private readonly SKPath _contourPath = new();

        public FocusOverlayRenderer()
        {
            _dimPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _ambientBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10f);
            _bloomBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.8f);

            // Слой 3: еле уловимый атмосферный отсвет (пик ~12%)
            _ambientHaloPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 11f,
                IsAntialias = true,
                MaskFilter = _ambientBlurFilter
            };

            // Слой 2: узкое оптическое рассеивание вдоль грани (пик ~38%)
            _opticalBloomPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3.5f,
                IsAntialias = true,
                MaskFilter = _bloomBlurFilter
            };

            // Слой 1: острая как бритва световая нить строго по геометрии окна
            _specularHairlinePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.25f,
                IsAntialias = true
            };
        }

        public void Render(
            SKCanvas canvas,
            int canvasWidth,
            int canvasHeight,
            SKRoundRect targetWindowRect,
            float dimOpacity,
            float glowAlpha,
            float glowProgress)
        {
            canvas.Clear(SKColors.Transparent);

            // Затемнение всего пространства вокруг активного окна
            if (dimOpacity > 0.001f)
            {
                canvas.Save();
                canvas.ClipRoundRect(targetWindowRect, SKClipOperation.Difference, antialias: true);

                byte alphaByte = (byte)Math.Clamp((int)(dimOpacity * 255f), 0, 255);
                _dimPaint.Color = new SKColor(0, 0, 0, alphaByte);
                canvas.DrawRect(0, 0, canvasWidth, canvasHeight, _dimPaint);
                canvas.Restore();
            }

            // Оптический световой импульс по грани окна
            if (glowAlpha > 0.001f && targetWindowRect.Width > 10 && targetWindowRect.Height > 10)
            {
                DrawOpticalSweep(canvas, targetWindowRect, glowAlpha, glowProgress);
            }
        }

        private void DrawOpticalSweep(
            SKCanvas canvas,
            SKRoundRect targetRect,
            float glowAlpha,
            float glowProgress)
        {
            _contourPath.Rewind();
            _contourPath.AddRoundRect(targetRect, SKPathDirection.Clockwise);

            // Асимметричное замедление в духе macOS / Apple Intelligence
            float easedProgress = CubicBezier.AppleDecel.Evaluate(glowProgress);
            float rotationAngle = -55f + easedProgress * 360f;

            var center = new SKPoint(targetRect.Rect.MidX, targetRect.Rect.MidY);
            var matrix = SKMatrix.CreateRotationDegrees(rotationAngle, center.X, center.Y);

            using var shader = SKShader.CreateSweepGradient(center, OpticalColors, OpticalPositions, matrix);

            _ambientHaloPaint.Shader = shader;
            _ambientHaloPaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(0.13f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(_contourPath, _ambientHaloPaint);

            _opticalBloomPaint.Shader = shader;
            _opticalBloomPaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(0.38f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(_contourPath, _opticalBloomPaint);

            _specularHairlinePaint.Shader = shader;
            _specularHairlinePaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(0.92f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(_contourPath, _specularHairlinePaint);

            _ambientHaloPaint.Shader = null;
            _opticalBloomPaint.Shader = null;
            _specularHairlinePaint.Shader = null;
        }

        public void Dispose()
        {
            _contourPath.Dispose();
            _dimPaint.Dispose();
            _ambientHaloPaint.Dispose();
            _opticalBloomPaint.Dispose();
            _specularHairlinePaint.Dispose();
            _ambientBlurFilter.Dispose();
            _bloomBlurFilter.Dispose();
        }
    }
}
