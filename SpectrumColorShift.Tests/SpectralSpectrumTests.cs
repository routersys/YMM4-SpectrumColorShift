using System.Numerics;

namespace SpectrumColorShift.Tests;

public sealed class SpectralSpectrumTests
{
    static Vector4[] Table(int steps, float reference, DispersionLaw law)
    {
        var table = new Vector4[Spectrum.SpectralTableLength];
        Spectrum.FillSpectral(table, steps, reference, law);
        return table;
    }

    static Vector4 Gain(Vector4[] table, int index) => table[index * Spectrum.EntriesPerStep];

    static Vector4 Basis(Vector4[] table, int index) => table[index * Spectrum.EntriesPerStep + 1];

    static double Wavelength(int index, int steps) => 400d + 300d * index / (steps - 1);

    static double Offset(DispersionLaw law, double wavelength, double reference)
        => law == DispersionLaw.Prism ? 1d / (wavelength * wavelength) - 1d / (reference * reference) : reference - wavelength;

    static (double X, double Y, double Z) Integrate(Func<int, double> spectrum)
    {
        double x = 0d, y = 0d, z = 0d;
        for (var index = 0; index < SpectralBasis.Count; index++)
        {
            var wavelength = SpectralBasis.First + SpectralBasis.Interval * index;
            var (cx, cy, cz) = Spectrum.ColorMatching(wavelength);
            var weight = spectrum(index) * SpectralBasis.IlluminantAt(wavelength) * (index == 0 || index == SpectralBasis.Count - 1 ? 0.5 : 1d);
            x += weight * cx;
            y += weight * cy;
            z += weight * cz;
        }

        return (x, y, z);
    }

    static double[] RgbOf(ReadOnlySpan<double> xyz, double[] white)
    {
        var primaries = new (double X, double Y)[] { (0.64, 0.33), (0.30, 0.60), (0.15, 0.06) };
        var columns = primaries.Select(p => new[] { p.X / p.Y, 1d, (1d - p.X - p.Y) / p.Y }).ToArray();
        var scale = Solve(columns, white);
        var matrix = columns.Select((column, c) => column.Select(value => value * scale[c]).ToArray()).ToArray();

        return Solve(matrix, xyz.ToArray());
    }

    static double[] Solve(double[][] columns, double[] target)
    {
        double[,] m = new double[3, 3];
        for (var c = 0; c < 3; c++)
            for (var r = 0; r < 3; r++)
                m[r, c] = columns[c][r];

        var det = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
        var result = new double[3];
        for (var c = 0; c < 3; c++)
        {
            var t = (double[,])m.Clone();
            for (var r = 0; r < 3; r++)
                t[r, c] = target[r];

            result[c] = (t[0, 0] * (t[1, 1] * t[2, 2] - t[1, 2] * t[2, 1]) - t[0, 1] * (t[1, 0] * t[2, 2] - t[1, 2] * t[2, 0]) + t[0, 2] * (t[1, 0] * t[2, 1] - t[1, 1] * t[2, 0])) / det;
        }

        return result;
    }

    static Vector3 BasisAt(int index) => SpectralBasis.At(SpectralBasis.First + SpectralBasis.Interval * index);

    [Fact]
    public void TheIlluminantIsNormalizedTo100AtFiveHundredSixtyNanometers()
    {
        Assert.Equal(100d, SpectralBasis.IlluminantAt(560d), 1e-4);
        Assert.Equal(82.7549d, SpectralBasis.IlluminantAt(400d), 1e-4);
        Assert.Equal(71.6091d, SpectralBasis.IlluminantAt(700d), 1e-4);
    }

    [Fact]
    public void TheIlluminantHasTheChromaticityOfD65()
    {
        var (x, y, z) = Integrate(_ => 1d);
        var sum = x + y + z;

        Assert.Equal(0.3127d, x / sum, 1e-3);
        Assert.Equal(0.3290d, y / sum, 1e-3);
    }

    [Fact]
    public void TheBasisSpectraAreNeverNegativeAndSumToOneAtEveryWavelength()
    {
        for (var index = 0; index < SpectralBasis.Count; index++)
        {
            var basis = BasisAt(index);

            Assert.True(basis.X >= 0f && basis.Y >= 0f && basis.Z >= 0f, $"{index}");
            Assert.Equal(1d, basis.X + basis.Y + basis.Z, 1e-6);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheBasisSpectraReproduceTheSrgbPrimaries(int channel)
    {
        var white = Integrate(_ => 1d);
        var normalization = Integrate(_ => 1d).Y;
        var whiteXyz = new[] { white.X / normalization, white.Y / normalization, white.Z / normalization };
        var xyz = Integrate(index => channel switch { 0 => BasisAt(index).X, 1 => BasisAt(index).Y, _ => BasisAt(index).Z });
        var rgb = RgbOf([xyz.X / normalization, xyz.Y / normalization, xyz.Z / normalization], whiteXyz);

        for (var component = 0; component < 3; component++)
            Assert.Equal(component == channel ? 1d : 0d, rgb[component], 1e-5);
    }

    [Theory]
    [InlineData(0, 600d, 700d)]
    [InlineData(1, 500d, 580d)]
    [InlineData(2, 400d, 480d)]
    public void EachBasisSpectrumLivesInItsOwnPartOfTheSpectrum(int channel, double from, double to)
    {
        var peak = Enumerable.Range(0, SpectralBasis.Count)
            .MaxBy(index => channel switch { 0 => BasisAt(index).X, 1 => BasisAt(index).Y, _ => BasisAt(index).Z });

        Assert.InRange(SpectralBasis.First + SpectralBasis.Interval * peak, from, to);
    }

    [Fact]
    public void TheBasisIsSmoothEnoughForTheSampledSpectrum()
    {
        for (var channel = 0; channel < 3; channel++)
            for (var index = 1; index < SpectralBasis.Count - 1; index++)
            {
                var before = BasisAt(index - 1);
                var here = BasisAt(index);
                var after = BasisAt(index + 1);
                var bend = channel switch
                {
                    0 => before.X - 2f * here.X + after.X,
                    1 => before.Y - 2f * here.Y + after.Y,
                    _ => before.Z - 2f * here.Z + after.Z,
                };

                Assert.InRange(Math.Abs(bend), 0f, 0.05f);
            }
    }

    [Fact]
    public void TheBasisInterpolatesLinearlyBetweenItsSamples()
    {
        var left = BasisAt(10);
        var right = BasisAt(11);
        var middle = SpectralBasis.At(SpectralBasis.First + SpectralBasis.Interval * 10.5);

        Assert.Equal((left.X + right.X) / 2f, middle.X, 1e-6f);
        Assert.Equal((left.Y + right.Y) / 2f, middle.Y, 1e-6f);
        Assert.Equal((left.Z + right.Z) / 2f, middle.Z, 1e-6f);
        Assert.Equal(BasisAt(0), SpectralBasis.At(100d));
        Assert.Equal(BasisAt(SpectralBasis.Count - 1), SpectralBasis.At(900d));
    }

    [Theory]
    [InlineData(8, 550f, DispersionLaw.Grating)]
    [InlineData(32, 550f, DispersionLaw.Grating)]
    [InlineData(64, 550f, DispersionLaw.Grating)]
    [InlineData(13, 400f, DispersionLaw.Prism)]
    [InlineData(40, 700f, DispersionLaw.Prism)]
    [InlineData(25, 620f, DispersionLaw.Prism)]
    public void TheGainsAndTheBasisReproduceEveryColorWhenNothingMoves(int steps, float reference, DispersionLaw law)
    {
        var table = Table(steps, reference, law);

        var identity = new double[3, 3];
        for (var index = 0; index < steps; index++)
        {
            var gain = Gain(table, index);
            var basis = Basis(table, index);
            double[] g = [gain.X, gain.Y, gain.Z];
            double[] b = [basis.X, basis.Y, basis.Z];
            for (var row = 0; row < 3; row++)
                for (var column = 0; column < 3; column++)
                    identity[row, column] += g[row] * b[column];
        }

        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
                Assert.Equal(row == column ? 1d : 0d, identity[row, column], 2e-5);
    }

    [Fact]
    public void EveryStepCountFromEightToSixtyFourReproducesWhiteAndKeepsTheCoverageNormalized()
    {
        for (var steps = Spectrum.MinSteps; steps <= Spectrum.MaxSteps; steps++)
        {
            var table = Table(steps, 550f, DispersionLaw.Grating);
            var red = 0d;
            var green = 0d;
            var blue = 0d;
            var coverage = 0d;
            for (var index = 0; index < steps; index++)
            {
                var gain = Gain(table, index);
                var basis = Basis(table, index);
                var amplitude = basis.X + basis.Y + basis.Z;
                red += gain.X * amplitude;
                green += gain.Y * amplitude;
                blue += gain.Z * amplitude;
                coverage += gain.W;
            }

            Assert.True(Math.Abs(red - 1d) < 2e-5 && Math.Abs(green - 1d) < 2e-5 && Math.Abs(blue - 1d) < 2e-5, $"{steps} steps");
            Assert.Equal(1d, coverage, 1e-5);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(63)]
    public void EntriesBeyondTheStepsAreCleared(int steps)
    {
        var table = Enumerable.Repeat(new Vector4(9f), Spectrum.SpectralTableLength).ToArray();

        Spectrum.FillSpectral(table, steps, 550f, DispersionLaw.Grating);

        Assert.All(table.Skip(steps * Spectrum.EntriesPerStep), entry => Assert.Equal(Vector4.Zero, entry));
        Assert.All(table.Take(steps * Spectrum.EntriesPerStep), entry => Assert.NotEqual(new Vector4(9f), entry));
    }

    [Theory]
    [InlineData(DispersionLaw.Grating, 550f)]
    [InlineData(DispersionLaw.Grating, 450f)]
    [InlineData(DispersionLaw.Grating, 650f)]
    [InlineData(DispersionLaw.Prism, 550f)]
    [InlineData(DispersionLaw.Prism, 400f)]
    [InlineData(DispersionLaw.Prism, 700f)]
    public void TheShiftCoefficientFollowsTheDispersionLaw(DispersionLaw law, float reference)
    {
        const int steps = 24;
        var table = Table(steps, reference, law);
        var norm = Math.Max(Math.Abs(Offset(law, 400d, reference)), Math.Abs(Offset(law, 700d, reference)));

        for (var index = 0; index < steps; index++)
            Assert.Equal(Offset(law, Wavelength(index, steps), reference) / norm, Basis(table, index).W, 1e-5);
    }

    [Theory]
    [InlineData(DispersionLaw.Grating, 550f, 1f, -1f)]
    [InlineData(DispersionLaw.Grating, 650f, 1f, -0.2f)]
    [InlineData(DispersionLaw.Grating, 450f, 0.2f, -1f)]
    [InlineData(DispersionLaw.Grating, 400f, 0f, -1f)]
    [InlineData(DispersionLaw.Grating, 700f, 1f, 0f)]
    [InlineData(DispersionLaw.Prism, 700f, 1f, 0f)]
    public void TheSpectrumEndsReachAtMostTheFullDistance(DispersionLaw law, float reference, float shortEnd, float longEnd)
    {
        var table = Table(16, reference, law);

        Assert.Equal(shortEnd, Basis(table, 0).W, 1e-5);
        Assert.Equal(longEnd, Basis(table, 15).W, 1e-5);
    }

    [Theory]
    [InlineData(DispersionLaw.Grating)]
    [InlineData(DispersionLaw.Prism)]
    public void TheShiftCoefficientFallsAsTheWavelengthGrows(DispersionLaw law)
    {
        var table = Table(Spectrum.MaxSteps, 550f, law);

        for (var index = 1; index < Spectrum.MaxSteps; index++)
            Assert.True(Basis(table, index).W < Basis(table, index - 1).W, $"{index}");
    }

    [Theory]
    [InlineData(DispersionLaw.Grating)]
    [InlineData(DispersionLaw.Prism)]
    public void TheSampleAtTheReferenceWavelengthDoesNotMove(DispersionLaw law)
    {
        var table = Table(13, 550f, law);

        Assert.Equal(0f, Basis(table, 6).W);
        Assert.NotEqual(0f, Basis(table, 5).W);
        Assert.NotEqual(0f, Basis(table, 7).W);
    }

    [Fact]
    public void ThePrismLawSpreadsBlueMoreThanRed()
    {
        var table = Table(16, 550f, DispersionLaw.Prism);

        Assert.InRange(-Basis(table, 15).W, 0.40f, 0.46f);
        Assert.True(Basis(table, 0).W > -Basis(table, 15).W * 2f);
    }

    [Fact]
    public void TheTwoLawsGiveDifferentCoefficients()
    {
        var grating = Table(24, 550f, DispersionLaw.Grating);
        var prism = Table(24, 550f, DispersionLaw.Prism);

        Assert.NotEqual(Basis(grating, 5).W, Basis(prism, 5).W);
        Assert.NotEqual(Basis(grating, 18).W, Basis(prism, 18).W);
    }

    [Fact]
    public void TheCoverageWeightsFollowTheLuminousEfficiency()
    {
        var table = Table(32, 550f, DispersionLaw.Grating);
        var centroid = Enumerable.Range(0, 32).Sum(index => Wavelength(index, 32) * Gain(table, index).W);

        Assert.InRange(centroid, 548d, 560d);
        Assert.True(Gain(table, 0).W < Gain(table, 16).W / 100f);
        Assert.True(Gain(table, 31).W < Gain(table, 16).W / 10f);
    }

    [Theory]
    [InlineData(300f, 400f)]
    [InlineData(400f, 400f)]
    [InlineData(550f, 550f)]
    [InlineData(700f, 700f)]
    [InlineData(900f, 700f)]
    [InlineData(float.PositiveInfinity, 700f)]
    [InlineData(float.NegativeInfinity, 400f)]
    [InlineData(float.NaN, 550f)]
    public void TheReferenceStaysWithinTheSpectrum(float requested, float expected)
    {
        Assert.Equal(expected, Spectrum.ClampReference(requested));
        Assert.Equal(Table(16, expected, DispersionLaw.Grating), Table(16, requested, DispersionLaw.Grating));
    }

    [Theory]
    [InlineData(int.MinValue, 8)]
    [InlineData(9, 12)]
    [InlineData(33, 36)]
    [InlineData(61, 64)]
    [InlineData(int.MaxValue, 64)]
    public void ThePaddedStepsFillWholeUnrolledIterations(int requested, int expected)
    {
        Assert.Equal(expected, Spectrum.Padded(requested));
        Assert.Equal(0, Spectrum.Padded(requested) % Spectrum.Unroll);
    }

    [Theory]
    [InlineData(int.MinValue, 8)]
    [InlineData(7, 8)]
    [InlineData(8, 8)]
    [InlineData(40, 40)]
    [InlineData(64, 64)]
    [InlineData(65, 64)]
    [InlineData(int.MaxValue, 64)]
    public void TheStepsStayWithinTheirRange(int requested, int expected)
    {
        Assert.Equal(expected, Spectrum.ClampSteps(requested));
        Assert.Equal(Table(expected, 550f, DispersionLaw.Grating), Table(requested, 550f, DispersionLaw.Grating));
    }
}
