using System.Numerics;

namespace SpectrumColorShift
{
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

        public static Vector3 Weights(double wavelength)
        {
            var x = 1.056 * Lobe(wavelength, 599.8, 37.9, 31.0)
                + 0.362 * Lobe(wavelength, 442.0, 16.0, 26.7)
                - 0.065 * Lobe(wavelength, 501.1, 20.4, 26.2);
            var y = 0.821 * Lobe(wavelength, 568.8, 46.9, 40.5)
                + 0.286 * Lobe(wavelength, 530.9, 16.3, 31.1);
            var z = 1.217 * Lobe(wavelength, 437.0, 11.8, 36.0)
                + 0.681 * Lobe(wavelength, 459.0, 26.0, 13.8);

            return new Vector3(
                (float)Math.Max(3.2404542 * x - 1.5371385 * y - 0.4985314 * z, 0.0),
                (float)Math.Max(-0.9692660 * x + 1.8760108 * y + 0.0415560 * z, 0.0),
                (float)Math.Max(0.0556434 * x - 0.2040259 * y + 1.0572252 * z, 0.0));
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

        private static double Lobe(double wavelength, double center, double lowSigma, double highSigma)
        {
            var t = (wavelength - center) / (wavelength < center ? lowSigma : highSigma);
            return Math.Exp(-0.5 * t * t);
        }
    }
}
