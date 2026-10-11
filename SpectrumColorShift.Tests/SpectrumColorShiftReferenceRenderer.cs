using System.Numerics;

namespace SpectrumColorShift.Tests;

internal sealed record ShiftSettings(
    ShiftMode Mode = ShiftMode.Linear,
    float Distance = 8f,
    float Angle = 0f,
    Vector2 Center = default,
    float Strength = 1f,
    DispersionLaw Dispersion = DispersionLaw.Grating,
    float ReferenceWavelength = 550f,
    int Steps = 32)
{
    public void Apply(SpectrumColorShiftCustomEffect effect)
    {
        effect.Mode = (int)Mode;
        effect.Distance = Distance;
        effect.Angle = Angle;
        effect.CenterX = Center.X;
        effect.CenterY = Center.Y;
        effect.Strength = Strength;
        effect.Dispersion = (int)Dispersion;
        effect.ReferenceWavelength = ReferenceWavelength;
        effect.Steps = Steps;
    }
}

internal sealed class ShiftReferenceRenderer
{
    readonly SourceImage source;
    readonly ShiftSettings settings;
    readonly Vector4[] table = new Vector4[Spectrum.MaxSteps];
    readonly Vector2 direction;
    readonly Vector2 center;
    readonly float radius;
    readonly int steps;

    public ShiftReferenceRenderer(SourceImage source, ShiftSettings settings)
    {
        this.source = source;
        this.settings = settings;
        steps = Spectrum.ClampSteps(settings.Steps);
        Spectrum.Fill(table, steps, settings.ReferenceWavelength, settings.Dispersion);
        var radians = settings.Angle * MathF.PI / 180f;
        direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        var rect = new Vortice.RawRect(0, 0, source.Width, source.Height);
        center = ShiftGeometry.Center(rect, settings.Center);
        radius = ShiftGeometry.Radius(rect, center);
    }

    public Vector4 Pixel(int x, int y)
    {
        var position = new Vector2(x + 0.5f, y + 0.5f);
        var original = Covered(position);
        if (settings.Distance == 0f || settings.Strength <= 0f)
            return original;

        var color = Vector3.Zero;
        var coverage = Vector3.Zero;
        for (var index = 0; index < steps; index++)
        {
            var entry = table[index];
            var target = ShiftReference.SamplePosition(settings.Mode, position, entry.W, settings.Distance, direction, center, radius);
            var shifted = Covered(target);
            var weights = new Vector3(entry.X, entry.Y, entry.Z);
            color += weights * new Vector3(shifted.X, shifted.Y, shifted.Z);
            coverage += weights * shifted.W;
        }

        var alpha = MathF.Max(coverage.X, MathF.Max(coverage.Y, coverage.Z));
        var result = new Vector4(Vector3.Min(color, new Vector3(alpha)), alpha);

        return Vector4.Lerp(original, result, Math.Clamp(settings.Strength, 0f, 1f));
    }

    Vector4 Covered(Vector2 target)
    {
        var size = new Vector2(source.Width, source.Height);
        var clamped = Vector2.Clamp(target, new Vector2(0.5f), size - new Vector2(0.5f));
        var edge = Vector2.Min(target, size - target) + new Vector2(0.5f);
        var coverage = Vector2.Clamp(edge, Vector2.Zero, Vector2.One);

        return Bilinear(clamped) * (coverage.X * coverage.Y);
    }

    Vector4 Bilinear(Vector2 position)
    {
        var shifted = position - new Vector2(0.5f);
        var x = (int)MathF.Floor(shifted.X);
        var y = (int)MathF.Floor(shifted.Y);
        var fraction = new Vector2(shifted.X - x, shifted.Y - y);

        return Vector4.Lerp(
            Vector4.Lerp(Texel(x, y), Texel(x + 1, y), fraction.X),
            Vector4.Lerp(Texel(x, y + 1), Texel(x + 1, y + 1), fraction.X),
            fraction.Y);
    }

    Vector4 Texel(int x, int y)
    {
        var pixel = source[Math.Clamp(x, 0, source.Width - 1), Math.Clamp(y, 0, source.Height - 1)];

        return new Vector4(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha) / 255f;
    }
}
