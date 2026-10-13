using System.Numerics;

namespace SpectrumColorShift;

internal static class SpectralFilter
{
    public const int EntriesPerTap = 3;
    public const int TapQuantum = 4;
    public const int MaxTaps = 44;
    public const int TableLength = MaxTaps * EntriesPerTap;
    public const float MaxTapShare = 0.65f;

    private const float AxisTolerance = 1e-5f;
    private const int Channels = 3;
    private const int ValuesPerTap = Channels * Channels + 1;

    public readonly record struct Layout(int Taps, int Origin, bool Vertical);

    private readonly record struct Placement(int Floor, double Fraction);

    public static Layout Fill(Span<Vector4> table, ReadOnlySpan<Vector4> spectral, int steps, float distance, Vector2 direction)
    {
        if (distance == 0f || !TryAxis(direction, out var vertical, out var along))
            return default;

        steps = Spectrum.ClampSteps(steps);
        Span<Placement> placements = stackalloc Placement[Spectrum.MaxSteps];
        var layout = Plan(spectral, steps, distance * along, vertical, placements);
        if (layout.Taps == 0)
            return default;

        Span<double> sums = stackalloc double[layout.Taps * ValuesPerTap];
        sums.Clear();
        for (var step = 0; step < steps; step++)
        {
            var spectralStep = SpectralStep.Read(spectral, step);
            var placement = placements[step];
            Add(sums, placement.Floor - layout.Origin, 1d - placement.Fraction, spectralStep);
            Add(sums, placement.Floor + 1 - layout.Origin, placement.Fraction, spectralStep);
        }

        Write(table, sums, layout.Taps);

        return layout;
    }

    private static Layout Plan(ReadOnlySpan<Vector4> spectral, int steps, float signedDistance, bool vertical, Span<Placement> placements)
    {
        var low = int.MaxValue;
        var high = int.MinValue;
        for (var step = 0; step < steps; step++)
        {
            var offset = -(double)SpectralStep.Read(spectral, step).Displacement * signedDistance;
            var floor = (int)Math.Floor(offset);
            placements[step] = new Placement(floor, offset - floor);
            low = Math.Min(low, floor);
            high = Math.Max(high, floor + 1);
        }

        var taps = Quantum.RoundUp(high - low + 1, TapQuantum);

        return taps <= MaxTaps && taps <= Spectrum.Padded(steps) * MaxTapShare ? new Layout(taps, low, vertical) : default;
    }

    private static bool TryAxis(Vector2 direction, out bool vertical, out float along)
    {
        vertical = false;
        along = direction.X;
        if (MathF.Abs(direction.Y) <= AxisTolerance)
            return true;

        vertical = true;
        along = direction.Y;
        return MathF.Abs(direction.X) <= AxisTolerance;
    }

    private static void Add(Span<double> sums, int tap, double weight, SpectralStep step)
    {
        var values = sums.Slice(tap * ValuesPerTap, ValuesPerTap);
        for (var row = 0; row < Channels; row++)
        {
            for (var column = 0; column < Channels; column++)
                values[row * Channels + column] += weight * step.Gain[row] * step.Basis[column];
        }

        values[Channels * Channels] += weight * step.AlphaGain;
    }

    private static void Write(Span<Vector4> table, ReadOnlySpan<double> sums, int taps)
    {
        for (var tap = 0; tap < taps; tap++)
        {
            var values = sums.Slice(tap * ValuesPerTap, ValuesPerTap);
            table[tap * EntriesPerTap] = new Vector4((float)values[0], (float)values[1], (float)values[2], (float)values[Channels * Channels]);
            table[tap * EntriesPerTap + 1] = new Vector4((float)values[3], (float)values[4], (float)values[5], 0f);
            table[tap * EntriesPerTap + 2] = new Vector4((float)values[6], (float)values[7], (float)values[8], 0f);
        }

        table[(taps * EntriesPerTap)..].Clear();
    }
}
