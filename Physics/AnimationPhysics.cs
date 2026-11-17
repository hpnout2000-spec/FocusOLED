using System;

namespace FocusGlow.Physics
{
    /// <summary>
    /// Exact implementation of cubic-bezier curves matching CSS / Apple fluid transitions.
    /// Uses Newton-Raphson approximation for parameter inversion.
    /// </summary>
    public sealed class CubicBezier
    {
        private readonly double _x1;
        private readonly double _y1;
        private readonly double _x2;
        private readonly double _y2;

        /// <summary>
        /// Asymmetric deceleration curve: cubic-bezier(0.22, 1, 0.36, 1) - Apple Intelligence / iOS style.
        /// </summary>
        public static readonly CubicBezier AppleDecel = new(0.22, 1.0, 0.36, 1.0);

        /// <summary>
        /// Standard smooth ease out: cubic-bezier(0.0, 0.0, 0.2, 1.0).
        /// </summary>
        public static readonly CubicBezier EaseOut = new(0.0, 0.0, 0.2, 1.0);

        /// <summary>
        /// Fast out, slow in: cubic-bezier(0.4, 0.0, 0.2, 1.0).
        /// </summary>
        public static readonly CubicBezier Standard = new(0.4, 0.0, 0.2, 1.0);

        public CubicBezier(double x1, double y1, double x2, double y2)
        {
            _x1 = Math.Clamp(x1, 0.0, 1.0);
            _y1 = y1;
            _x2 = Math.Clamp(x2, 0.0, 1.0);
            _y2 = y2;
        }

        public double Evaluate(double x)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;

            double t = SolveT(x);
            return SampleY(t);
        }

        public float Evaluate(float x) => (float)Evaluate((double)x);

        private double SampleX(double t)
        {
            // B(t) = 3*(1-t)^2 * t * x1 + 3*(1-t) * t^2 * x2 + t^3
            double oneMinusT = 1.0 - t;
            return 3.0 * oneMinusT * oneMinusT * t * _x1 +
                   3.0 * oneMinusT * t * t * _x2 +
                   t * t * t;
        }

        private double SampleY(double t)
        {
            double oneMinusT = 1.0 - t;
            return 3.0 * oneMinusT * oneMinusT * t * _y1 +
                   3.0 * oneMinusT * t * t * _y2 +
                   t * t * t;
        }

        private double SampleDerivativeX(double t)
        {
            double oneMinusT = 1.0 - t;
            return 3.0 * oneMinusT * oneMinusT * _x1 +
                   6.0 * oneMinusT * t * (_x2 - _x1) +
                   3.0 * t * t * (1.0 - _x2);
        }

        private double SolveT(double x)
        {
            // Initial guess
            double t = x;

            // 6 iterations of Newton-Raphson
            for (int i = 0; i < 6; i++)
            {
                double currentX = SampleX(t) - x;
                if (Math.Abs(currentX) < 1e-6)
                    return t;

                double dX = SampleDerivativeX(t);
                if (Math.Abs(dX) < 1e-6)
                    break;

                t -= currentX / dX;
                t = Math.Clamp(t, 0.0, 1.0);
            }

            // Fallback bisection if Newton didn't converge tightly
            double t0 = 0.0;
            double t1 = 1.0;
            t = x;

            for (int i = 0; i < 8; i++)
            {
                double currentX = SampleX(t);
                if (Math.Abs(currentX - x) < 1e-5)
                    return t;

                if (x > currentX)
                    t0 = t;
                else
                    t1 = t;

                t = (t1 + t0) * 0.5;
            }

            return t;
        }
    }

    /// <summary>
    /// Physics-based damped harmonic oscillator (damped spring).
    /// Seamlessly continues from current value and velocity when interrupted.
    /// </summary>
    public sealed class DampedSpring
    {
        public double Value { get; set; }
        public double Velocity { get; set; }
        public double Target { get; set; }

        /// <summary>
        /// Natural angular frequency (stiffness). Default = 28.0 (fast responsive spring).
        /// </summary>
        public double Frequency { get; set; }

        /// <summary>
        /// Damping ratio (zeta).
        /// 1.0 = critically damped (no overshoot, fastest settling).
        /// &gt; 1.0 = overdamped.
        /// &lt; 1.0 = underdamped (bouncy).
        /// Default = 1.0.
        /// </summary>
        public double DampingRatio { get; set; }

        public DampedSpring(double initialValue = 0.0, double frequency = 28.0, double dampingRatio = 1.0)
        {
            Value = initialValue;
            Target = initialValue;
            Velocity = 0.0;
            Frequency = frequency;
            DampingRatio = dampingRatio;
        }

        public void Reset(double value)
        {
            Value = value;
            Target = value;
            Velocity = 0.0;
        }

        public void Update(double dt)
        {
            if (dt <= 0.0) return;
            // Clamp dt to avoid instability on huge frame drops
            if (dt > 0.05) dt = 0.05;

            // Semi-implicit Euler integration for second-order spring:
            // a = -omega^2 * (x - target) - 2 * zeta * omega * v
            double displacement = Value - Target;
            double omega = Frequency;
            double springForce = -(omega * omega) * displacement;
            double dampingForce = -(2.0 * DampingRatio * omega) * Velocity;
            double acceleration = springForce + dampingForce;

            Velocity += acceleration * dt;
            Value += Velocity * dt;

            // Snap when effectively settled
            if (Math.Abs(displacement) < 1e-4 && Math.Abs(Velocity) < 1e-3)
            {
                Value = Target;
                Velocity = 0.0;
            }
        }

        public bool IsSettled => Math.Abs(Value - Target) < 1e-4 && Math.Abs(Velocity) < 1e-3;
    }

    /// <summary>
    /// Smooth damp helper for float values.
    /// </summary>
    public static class SmoothDamp
    {
        public static float Update(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;

            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;

            float maxChange = maxSpeed * smoothTime;
            change = Math.Clamp(change, -maxChange, maxChange);
            target = current - change;

            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;

            if (originalTo - current > 0.0f == output > originalTo)
            {
                output = originalTo;
                currentVelocity = (output - originalTo) / deltaTime;
            }

            return output;
        }
    }
}
