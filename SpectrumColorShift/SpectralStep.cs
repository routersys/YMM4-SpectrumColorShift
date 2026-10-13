using System.Numerics;

namespace SpectrumColorShift;

internal readonly record struct SpectralStep(Vector3 Gain, float AlphaGain, Vector3 Basis, float Displacement)
{
    public static SpectralStep Read(ReadOnlySpan<Vector4> table, int step)
    {
        var gain = table[step * Spectrum.EntriesPerStep];
        var basis = table[step * Spectrum.EntriesPerStep + 1];

        return new SpectralStep(new Vector3(gain.X, gain.Y, gain.Z), gain.W, new Vector3(basis.X, basis.Y, basis.Z), basis.W);
    }

    public void Write(Span<Vector4> table, int step)
    {
        table[step * Spectrum.EntriesPerStep] = new Vector4(Gain, AlphaGain);
        table[step * Spectrum.EntriesPerStep + 1] = new Vector4(Basis, Displacement);
    }
}
