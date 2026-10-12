using System.Numerics;

namespace SpectrumColorShift.Tests;

internal sealed class SpectralReferenceRenderer
{
    readonly SourceImage source;
    readonly ShiftSettings settings;
    readonly Vector4[] table = new Vector4[Spectrum.SpectralTableLength];
    readonly Vector2 direction;
    readonly Vector2 center;
    readonly float radius;
    readonly int steps;

    public SpectralReferenceRenderer(SourceImage source, ShiftSettings settings)
    {
        this.source = source;
        this.settings = settings;
        steps = Spectrum.ClampSteps(settings.Steps);
        Spectrum.FillSpectral(table, steps, settings.ReferenceWavelength, settings.Dispersion);
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

        var (color, alpha) = Accumulated(position);
        var positive = Vector3.Max(color, Vector3.Zero);
        var resolved = Math.Clamp(Math.Max(alpha, Math.Max(positive.X, Math.Max(positive.Y, positive.Z))), 0f, 1f);
        var result = new Vector4(Vector3.SquareRoot(Vector3.Min(positive, new Vector3(resolved)) * resolved), resolved);

        return Vector4.Lerp(original, result, Math.Clamp(settings.Strength, 0f, 1f));
    }

    public (Vector3 Color, float Alpha) Accumulated(Vector2 position)
    {
        var color = Vector3.Zero;
        var alpha = 0f;
        for (var index = 0; index < steps; index++)
        {
            var gain = table[index * Spectrum.EntriesPerStep];
            var basis = table[index * Spectrum.EntriesPerStep + 1];
            var target = ShiftReference.SamplePosition(settings.Mode, position, basis.W, settings.Distance, direction, center, radius);
            var lit = Linear(target);
            color += new Vector3(gain.X, gain.Y, gain.Z) * Vector3.Dot(new Vector3(basis.X, basis.Y, basis.Z), new Vector3(lit.X, lit.Y, lit.Z));
            alpha += gain.W * lit.W;
        }

        return (color, alpha);
    }

    Vector4 Linear(Vector2 target)
    {
        var (raw, coverage) = Sample(target);
        if (raw.W <= 0f)
            return Vector4.Zero;

        var premultiplied = new Vector3(raw.X, raw.Y, raw.Z);

        return new Vector4(premultiplied * premultiplied / raw.W, raw.W) * coverage;
    }

    Vector4 Covered(Vector2 target)
    {
        var (raw, coverage) = Sample(target);

        return raw * coverage;
    }

    (Vector4 Raw, float Coverage) Sample(Vector2 target)
    {
        var size = new Vector2(source.Width, source.Height);
        var clamped = Vector2.Clamp(target, new Vector2(0.5f), size - new Vector2(0.5f));
        var edge = Vector2.Min(target, size - target) + new Vector2(0.5f);
        var coverage = Vector2.Clamp(edge, Vector2.Zero, Vector2.One);

        return (Bilinear(clamped), coverage.X * coverage.Y);
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
