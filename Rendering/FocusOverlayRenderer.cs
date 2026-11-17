using System;
using FocusGlow.Interop;
using FocusGlow.Physics;
using SkiaSharp;

namespace FocusGlow.Rendering
{
    public sealed class FocusOverlayRenderer : IDisposable
    {
        private static readonly SKColor[] AppleGeminiColors = new[]
        {
            new SKColor(0x42, 0x85, 0xF4), // Google Blue / Electric Azure
            new SKColor(0x9B, 0x72, 0xCB), // Gemini Purple / Orchid
            new SKColor(0xD9, 0x65, 0x70), // Coral Pink / Rose
            new SKColor(0xF4, 0xAF, 0x5F), // Amber Glow / Sunset
            new SKColor(0x42, 0x85, 0xF4)  // Seamless loop to start
        };

        private static readonly float[] ColorPositions = new[] { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };

        // Preallocated paints and filters for maximum GPU/rendering performance
        private readonly SKPaint _dimPaint;
        private readonly SKPaint _outerBloomPaint;
        private readonly SKPaint _midBloomPaint;
        private readonly SKPaint _coreGlowPaint;
        private readonly SKPaint _crispEdgePaint;

        private readonly SKMaskFilter _outerBlurFilter;
        private readonly SKMaskFilter _midBlurFilter;
        private readonly SKMaskFilter _coreBlurFilter;

        public FocusOverlayRenderer()
        {
            _dimPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _outerBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18f);
            _midBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);
            _coreBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.5f);

            _outerBloomPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 32f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                MaskFilter = _outerBlurFilter
            };

            _midBloomPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 16f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                MaskFilter = _midBlurFilter
            };

            _coreGlowPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 5f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                MaskFilter = _coreBlurFilter
            };

            _crispEdgePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
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
            // 1. Clear to transparent
            canvas.Clear(SKColors.Transparent);

            // 2. Render Phase 2: Background Dimming with active window cut out
            if (dimOpacity > 0.001f)
            {
                canvas.Save();
                // Clip out the target window using difference operation with antialiased rounded corners
                canvas.ClipRoundRect(targetWindowRect, SKClipOperation.Difference, antialias: true);

                byte alphaByte = (byte)Math.Clamp((int)(dimOpacity * 255f), 0, 255);
                _dimPaint.Color = new SKColor(0, 0, 0, alphaByte);

                canvas.DrawRect(0, 0, canvasWidth, canvasHeight, _dimPaint);
                canvas.Restore();
            }

            // 3. Render Phase 1: Gemini / Apple Intelligence contour glow ("comet" + multi-pass bloom)
            if (glowAlpha > 0.001f && targetWindowRect.Width > 10 && targetWindowRect.Height > 10)
            {
                DrawContourGlow(canvas, targetWindowRect, glowAlpha, glowProgress);
            }
        }

        private void DrawContourGlow(
            SKCanvas canvas,
            SKRoundRect targetRect,
            float glowAlpha,
            float glowProgress)
        {
            using var basePath = new SKPath();
            basePath.AddRoundRect(targetRect, SKPathDirection.Clockwise);

            using var measure = new SKPathMeasure(basePath, false);
            float totalLength = measure.Length;
            if (totalLength <= 1f) return;

            // Comet covers ~40% of the perimeter as a long fading tail
            float tailLength = totalLength * 0.40f;
            float headDist = glowProgress * totalLength * 1.35f; // sweeps 1.35 circuits
            float startDist = headDist - tailLength;

            using var cometPath = new SKPath();
            ExtractLoopSegment(measure, startDist, headDist, totalLength, cometPath);

            // Dynamic rotation for sweep gradient centered at active window
            var center = new SKPoint(targetRect.Rect.MidX, targetRect.Rect.MidY);
            float rotationAngle = (headDist / totalLength) * 360f;

            var localMatrix = SKMatrix.CreateRotationDegrees(rotationAngle, center.X, center.Y);
            using var shader = SKShader.CreateSweepGradient(center, AppleGeminiColors, ColorPositions, localMatrix);

            // Outer Bloom Pass (Soft atmospheric glow)
            _outerBloomPaint.Shader = shader;
            _outerBloomPaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(0.35f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(cometPath, _outerBloomPaint);

            // Mid Bloom Pass (Medium intense aura)
            _midBloomPaint.Shader = shader;
            _midBloomPaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(0.60f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(cometPath, _midBloomPaint);

            // Neon Core Laser Pass (Sharp vibrant beam)
            _coreGlowPaint.Shader = shader;
            _coreGlowPaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(0.95f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(cometPath, _coreGlowPaint);

            // Core Precision Highlight (Crisp ultra-thin ribbon)
            _crispEdgePaint.Shader = shader;
            _crispEdgePaint.Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)(1.0f * glowAlpha * 255f), 0, 255));
            canvas.DrawPath(cometPath, _crispEdgePaint);

            _outerBloomPaint.Shader = null;
            _midBloomPaint.Shader = null;
            _coreGlowPaint.Shader = null;
            _crispEdgePaint.Shader = null;
        }

        private static void ExtractLoopSegment(
            SKPathMeasure measure,
            float start,
            float end,
            float totalLength,
            SKPath dst)
        {
            if (totalLength <= 0.0f) return;

            // Wrap distances to [0, totalLength)
            float normStart = start;
            float normEnd = end;

            while (normStart < 0) normStart += totalLength;
            while (normEnd < 0) normEnd += totalLength;
            normStart %= totalLength;
            normEnd %= totalLength;

            if (normStart <= normEnd)
            {
                measure.GetSegment(normStart, normEnd, dst, true);
            }
            else
            {
                // Segment wraps across the seam
                measure.GetSegment(normStart, totalLength, dst, true);
                measure.GetSegment(0f, normEnd, dst, true);
            }
        }

        public void Dispose()
        {
            _dimPaint.Dispose();
            _outerBloomPaint.Dispose();
            _midBloomPaint.Dispose();
            _coreGlowPaint.Dispose();
            _crispEdgePaint.Dispose();

            _outerBlurFilter.Dispose();
            _midBlurFilter.Dispose();
            _coreBlurFilter.Dispose();
        }
    }
}
