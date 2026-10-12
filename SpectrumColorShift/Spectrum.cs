using System.Numerics;

namespace SpectrumColorShift;

internal static class Spectrum
{
    public const int MinSteps = 8;
    public const int MaxSteps = 64;
    public const int DefaultSteps = 32;
    public const float ShortestWavelength = 400f;
    public const float LongestWavelength = 700f;
    public const float DefaultReferenceWavelength = 550f;

    public static int ClampSteps(int steps) => Math.Clamp(steps, MinSteps, MaxSteps);

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
