using System.Numerics;

namespace SpectrumColorShift;

internal static class Spectrum
{
    public const int MinSteps = 8;
    public const int MaxSteps = 64;
    public const int DefaultSteps = 32;
    public const int EntriesPerStep = 2;
    public const int StepQuantum = 4;
    public const int SpectralTableLength = MaxSteps * EntriesPerStep;
    public const float ShortestWavelength = 400f;
    public const float LongestWavelength = 700f;
    public const float DefaultReferenceWavelength = 550f;

    public static int ClampSteps(int steps) => Math.Clamp(steps, MinSteps, MaxSteps);

    public static int Padded(int steps) => (ClampSteps(steps) + StepQuantum - 1) / StepQuantum * StepQuantum;

    public static float ClampReference(float wavelength)
        => float.IsNaN(wavelength) ? DefaultReferenceWavelength : Math.Clamp(wavelength, ShortestWavelength, LongestWavelength);

    public static (double X, double Y, double Z) ColorMatching(double wavelength)
    {
        var x = 0.362 * Lobe(wavelength, 442.0, 0.0624, 0.0374)
            + 1.056 * Lobe(wavelength, 599.8, 0.0264, 0.0323)
            - 0.065 * Lobe(wavelength, 501.1, 0.0490, 0.0382);
        var y = 0.821 * Lobe(wavelength, 568.8, 0.0213, 0.0247)
            + 0.286 * Lobe(wavelength, 530.9, 0.0613, 0.0322);
        var z = 1.217 * Lobe(wavelength, 437.0, 0.0845, 0.0278)
            + 0.681 * Lobe(wavelength, 459.0, 0.0385, 0.0725);

        return (x, y, z);
    }

    public static (double R, double G, double B) LinearSrgb(double x, double y, double z)
        => (3.2406255 * x - 1.5372080 * y - 0.4986286 * z,
            -0.9689307 * x + 1.8757561 * y + 0.0415175 * z,
            0.0557101 * x - 0.2040211 * y + 1.0569959 * z);

    public static Vector3 Weights(double wavelength)
    {
        var (x, y, z) = ColorMatching(wavelength);
        var (red, green, blue) = LinearSrgb(x, y, z);

        return new Vector3((float)Math.Max(red, 0.0), (float)Math.Max(green, 0.0), (float)Math.Max(blue, 0.0));
    }

    public static void Fill(Span<Vector4> table, int steps, float referenceWavelength, DispersionLaw law)
    {
        steps = ClampSteps(steps);
        var reference = ClampReference(referenceWavelength);
        var norm = Math.Max(
            Math.Abs(Displacement(law, ShortestWavelength, reference)),
            Math.Abs(Displacement(law, LongestWavelength, reference)));

        double sumRed = 0.0, sumGreen = 0.0, sumBlue = 0.0;
        for (var index = 0; index < steps; index++)
        {
            var wavelength = ShortestWavelength + (LongestWavelength - ShortestWavelength) * index / (steps - 1);
            var weights = Weights(wavelength);
            table[index] = new Vector4(weights, (float)(Displacement(law, wavelength, reference) / norm));
            sumRed += weights.X;
            sumGreen += weights.Y;
            sumBlue += weights.Z;
        }

        for (var index = 0; index < steps; index++)
        {
            var entry = table[index];
            table[index] = new Vector4((float)(entry.X / sumRed), (float)(entry.Y / sumGreen), (float)(entry.Z / sumBlue), entry.W);
        }

        table[steps..].Clear();
    }

    private static double Wavelength(int index, int steps)
        => ShortestWavelength + (LongestWavelength - ShortestWavelength) * index / (steps - 1);

    private static (double X, double Y, double Z) Emission(int index, int steps)
    {
        var wavelength = Wavelength(index, steps);
        var weight = (index == 0 || index == steps - 1 ? 0.5 : 1.0) * SpectralBasis.IlluminantAt(wavelength);
        var (x, y, z) = ColorMatching(wavelength);

        return (weight * x, weight * y, weight * z);
    }

    public static void FillSpectral(Span<Vector4> table, int steps, float referenceWavelength, DispersionLaw law)
    {
        steps = ClampSteps(steps);
        var reference = ClampReference(referenceWavelength);
        var norm = Math.Max(
            Math.Abs(Displacement(law, ShortestWavelength, reference)),
            Math.Abs(Displacement(law, LongestWavelength, reference)));

        Span<double> matrix = stackalloc double[9];
        Span<double> inverse = stackalloc double[9];
        matrix.Clear();
        var luminance = 0.0;
        for (var index = 0; index < steps; index++)
        {
            var (x, y, z) = Emission(index, steps);
            var basis = SpectralBasis.At(Wavelength(index, steps));
            matrix[0] += x * basis.X;
            matrix[1] += x * basis.Y;
            matrix[2] += x * basis.Z;
            matrix[3] += y * basis.X;
            matrix[4] += y * basis.Y;
            matrix[5] += y * basis.Z;
            matrix[6] += z * basis.X;
            matrix[7] += z * basis.Y;
            matrix[8] += z * basis.Z;
            luminance += y;
        }

        Invert(matrix, inverse);
        for (var index = 0; index < steps; index++)
        {
            var wavelength = Wavelength(index, steps);
            var (x, y, z) = Emission(index, steps);
            var basis = SpectralBasis.At(wavelength);
            var gain = new Vector3(
                (float)(inverse[0] * x + inverse[1] * y + inverse[2] * z),
                (float)(inverse[3] * x + inverse[4] * y + inverse[5] * z),
                (float)(inverse[6] * x + inverse[7] * y + inverse[8] * z));
            table[index * EntriesPerStep] = new Vector4(gain, (float)(y / luminance));
            table[index * EntriesPerStep + 1] = new Vector4(basis, (float)(Displacement(law, wavelength, reference) / norm));
        }

        table[(steps * EntriesPerStep)..].Clear();
    }

    private static void Invert(ReadOnlySpan<double> m, Span<double> inverse)
    {
        var c0 = m[4] * m[8] - m[5] * m[7];
        var c1 = m[5] * m[6] - m[3] * m[8];
        var c2 = m[3] * m[7] - m[4] * m[6];
        var scale = 1.0 / (m[0] * c0 + m[1] * c1 + m[2] * c2);
        inverse[0] = c0 * scale;
        inverse[1] = (m[2] * m[7] - m[1] * m[8]) * scale;
        inverse[2] = (m[1] * m[5] - m[2] * m[4]) * scale;
        inverse[3] = c1 * scale;
        inverse[4] = (m[0] * m[8] - m[2] * m[6]) * scale;
        inverse[5] = (m[2] * m[3] - m[0] * m[5]) * scale;
        inverse[6] = c2 * scale;
        inverse[7] = (m[1] * m[6] - m[0] * m[7]) * scale;
        inverse[8] = (m[0] * m[4] - m[1] * m[3]) * scale;
    }

    private static double Displacement(DispersionLaw law, double wavelength, double reference)
        => law == DispersionLaw.Prism
            ? 1.0 / (wavelength * wavelength) - 1.0 / (reference * reference)
            : reference - wavelength;

    private static double Lobe(double wavelength, double center, double lowScale, double highScale)
    {
        var t = (wavelength - center) * (wavelength < center ? lowScale : highScale);
        return Math.Exp(-0.5 * t * t);
    }
}
