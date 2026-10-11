using System.Numerics;

namespace SpectrumColorShift.Tests;

internal static class ShiftReference
{
    public static Vector2 SamplePosition(ShiftMode mode, Vector2 position, float coefficient, float distance, Vector2 direction, Vector2 center, float radius)
    {
        var shift = coefficient * distance;
        var offset = position - center;
        switch (mode)
        {
            case ShiftMode.Radial:
                return center + offset * (1f - Math.Clamp(shift / radius, -0.5f, 0.5f));
            case ShiftMode.Rotational:
                var sine = MathF.Sin(shift / radius);
                var cosine = MathF.Cos(shift / radius);
                return center + new Vector2(cosine * offset.X + sine * offset.Y, cosine * offset.Y - sine * offset.X);
            default:
                return position - shift * direction;
        }
    }
}
