using System.Numerics;
using Vortice;

namespace SpectrumColorShift
{
    internal static class ShiftGeometry
    {
        public const float MaxRadialShift = 0.5f;
        public const int Guard = 2;

        private const double RoundingTolerance = 1e-3;

        public static Vector2 Center(RawRect rect, Vector2 offset)
            => new((rect.Left + rect.Right) * 0.5f + offset.X, (rect.Top + rect.Bottom) * 0.5f + offset.Y);

        public static float Radius(RawRect rect, Vector2 center) => Math.Max(Farthest(rect, center), 1f);

        public static RawRect Output(RawRect rect, ShiftMode mode, float distance, Vector2 direction, Vector2 center, float radius)
        {
            var reach = Math.Abs(distance);
            if (reach <= 0f)
                return rect;

            return mode switch
            {
                ShiftMode.Radial => Scaled(rect, center, Math.Min(reach / radius, MaxRadialShift)),
                ShiftMode.Rotational => Swept(rect, center, Math.Min(reach / radius, MathF.PI)),
                _ => Straight(rect, reach, direction),
            };
        }

        public static RawRect Input(RawRect output, ShiftMode mode, float distance, Vector2 direction, Vector2 center, float radius)
        {
            var reach = Math.Abs(distance);
            if (reach <= 0f)
                return output;

            switch (mode)
            {
                case ShiftMode.Radial:
                    var ratio = Math.Min(reach / radius, MaxRadialShift);
                    return Inflate(
                        output,
                        Margin(ratio * Math.Max(Math.Abs(output.Left - center.X), Math.Abs(output.Right - center.X))),
                        Margin(ratio * Math.Max(Math.Abs(output.Top - center.Y), Math.Abs(output.Bottom - center.Y))));
                case ShiftMode.Rotational:
                    var chord = 2f * MathF.Sin(Math.Min(reach / radius, MathF.PI) * 0.5f) * Farthest(output, center);
                    return Inflate(output, Margin(chord), Margin(chord));
                default:
                    return Straight(output, reach, direction);
            }
        }

        private static RawRect Straight(RawRect rect, float reach, Vector2 direction)
            => Inflate(rect, Margin(reach * Math.Abs(direction.X)), Margin(reach * Math.Abs(direction.Y)));

        private static RawRect Scaled(RawRect rect, Vector2 center, float ratio)
        {
            var near = 1f / (1f + ratio);
            var far = 1f / (1f - ratio);
            var left = rect.Left - center.X;
            var top = rect.Top - center.Y;
            var right = rect.Right - center.X;
            var bottom = rect.Bottom - center.Y;

            return Outward(
                center.X + Math.Min(left * near, left * far),
                center.Y + Math.Min(top * near, top * far),
                center.X + Math.Max(right * near, right * far),
                center.Y + Math.Max(bottom * near, bottom * far));
        }

        private static RawRect Swept(RawRect rect, Vector2 center, float angle)
        {
            var right = float.NegativeInfinity;
            var bottom = float.NegativeInfinity;
            var left = float.NegativeInfinity;
            var top = float.NegativeInfinity;

            Extend(rect.Left - center.X, rect.Top - center.Y, angle, ref right, ref bottom, ref left, ref top);
            Extend(rect.Right - center.X, rect.Top - center.Y, angle, ref right, ref bottom, ref left, ref top);
            Extend(rect.Left - center.X, rect.Bottom - center.Y, angle, ref right, ref bottom, ref left, ref top);
            Extend(rect.Right - center.X, rect.Bottom - center.Y, angle, ref right, ref bottom, ref left, ref top);

            return Outward(center.X - left, center.Y - top, center.X + right, center.Y + bottom);
        }

        private static void Extend(float x, float y, float angle, ref float right, ref float bottom, ref float left, ref float top)
        {
            var length = MathF.Sqrt(x * x + y * y);
            var heading = MathF.Atan2(y, x);
            right = Math.Max(right, Reach(length, heading, 0f, angle));
            bottom = Math.Max(bottom, Reach(length, heading, MathF.PI * 0.5f, angle));
            left = Math.Max(left, Reach(length, heading, MathF.PI, angle));
            top = Math.Max(top, Reach(length, heading, -MathF.PI * 0.5f, angle));
        }

        private static float Reach(float length, float heading, float axis, float angle)
        {
            var difference = Math.Abs(MathF.IEEERemainder(heading - axis, MathF.Tau));
            return length * MathF.Cos(Math.Max(difference - angle, 0f));
        }

        private static float Farthest(RawRect rect, Vector2 center)
        {
            var dx = Math.Max(Math.Abs(rect.Left - center.X), Math.Abs(rect.Right - center.X));
            var dy = Math.Max(Math.Abs(rect.Top - center.Y), Math.Abs(rect.Bottom - center.Y));
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private static int Margin(float distance) => Saturate((long)Math.Ceiling(distance - RoundingTolerance) + Guard);

        private static RawRect Inflate(RawRect rect, int marginX, int marginY)
            => new(
                Saturate((long)rect.Left - marginX),
                Saturate((long)rect.Top - marginY),
                Saturate((long)rect.Right + marginX),
                Saturate((long)rect.Bottom + marginY));

        private static RawRect Outward(float left, float top, float right, float bottom)
            => new(
                Saturate((long)Math.Floor(left) - Guard),
                Saturate((long)Math.Floor(top) - Guard),
                Saturate((long)Math.Ceiling(right) + Guard),
                Saturate((long)Math.Ceiling(bottom) + Guard));

        private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
