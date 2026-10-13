using System.Numerics;

namespace SpectrumColorShift;

internal static class SpectralFilter
{
    public const int EntriesPerTap = 3;
    public const int TapQuantum = 4;
    public const int MaxTaps = 44;
    public const int TableLength = MaxTaps * EntriesPerTap;

    private const float AxisTolerance = 1e-5f;
    private const int ShareNumerator = 13;
    private const int ShareDenominator = 20;
    private const int Channels = 3;
    private const int ValuesPerTap = Channels * Channels + 1;

    public static IEnumerable<int> Counts => Enumerable.Range(1, MaxTaps / TapQuantum).Select(index => index * TapQuantum);

    public readonly record struct Layout(int Taps, int Origin, bool Vertical);

    public static Layout Fill(Span<Vector4> table, ReadOnlySpan<Vector4> spectral, int steps, float distance, Vector2 direction)
    {
        if (distance == 0f || !TryAxis(direction, out var vertical, out var along))
            return default;

        steps = Spectrum.ClampSteps(steps);
        Span<int> floors = stackalloc int[Spectrum.MaxSteps];
        Span<double> fractions = stackalloc double[Spectrum.MaxSteps];
        var low = int.MaxValue;
        var high = int.MinValue;
        for (var step = 0; step < steps; step++)
        {
            var offset = -(double)spectral[step * Spectrum.EntriesPerStep + 1].W * distance * along;
            floors[step] = (int)Math.Floor(offset);
            fractions[step] = offset - floors[step];
            low = Math.Min(low, floors[step]);
            high = Math.Max(high, floors[step] + 1);
        }

        var taps = Padded(high - low + 1);
        if (taps > MaxTaps || taps * ShareDenominator > Spectrum.Padded(steps) * ShareNumerator)
            return default;

        Span<double> sums = stackalloc double[MaxTaps * ValuesPerTap];
        sums[..(taps * ValuesPerTap)].Clear();
        for (var step = 0; step < steps; step++)
        {
            var gain = spectral[step * Spectrum.EntriesPerStep];
            var basis = spectral[step * Spectrum.EntriesPerStep + 1];
            Add(sums, floors[step] - low, 1d - fractions[step], gain, basis);
            Add(sums, floors[step] + 1 - low, fractions[step], gain, basis);
        }

        for (var tap = 0; tap < taps; tap++)
        {
            var values = sums.Slice(tap * ValuesPerTap, ValuesPerTap);
            table[tap * EntriesPerTap] = new Vector4((float)values[0], (float)values[1], (float)values[2], (float)values[Channels * Channels]);
            table[tap * EntriesPerTap + 1] = new Vector4((float)values[3], (float)values[4], (float)values[5], 0f);
            table[tap * EntriesPerTap + 2] = new Vector4((float)values[6], (float)values[7], (float)values[8], 0f);
        }

        table[(taps * EntriesPerTap)..].Clear();

        return new Layout(taps, low, vertical);
    }

    private static int Padded(int taps) => (taps + TapQuantum - 1) / TapQuantum * TapQuantum;

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

    private static void Add(Span<double> sums, int tap, double weight, Vector4 gain, Vector4 basis)
    {
        var values = sums.Slice(tap * ValuesPerTap, ValuesPerTap);
        for (var row = 0; row < Channels; row++)
        {
            for (var column = 0; column < Channels; column++)
                values[row * Channels + column] += weight * Component(gain, row) * Component(basis, column);
        }

        values[Channels * Channels] += weight * gain.W;
    }

    private static float Component(Vector4 vector, int index) => index switch { 0 => vector.X, 1 => vector.Y, _ => vector.Z };
}
