using System.Numerics;

namespace SpectrumColorShift.Tests;

public sealed class SpectrumTests
{
    static Vector4[] Table(int steps, float reference, DispersionLaw law)
    {
        var table = new Vector4[Spectrum.MaxSteps];
        Spectrum.Fill(table, steps, reference, law);
        return table;
    }

    static double Wavelength(int index, int steps) => 400d + 300d * index / (steps - 1);

    static double Centroid(Vector4[] table, int steps, Func<Vector4, float> channel)
        => Enumerable.Range(0, steps).Sum(index => Wavelength(index, steps) * channel(table[index])) / Enumerable.Range(0, steps).Sum(index => channel(table[index]));

    static double Offset(DispersionLaw law, double wavelength, double reference)
        => law == DispersionLaw.Prism ? 1d / (wavelength * wavelength) - 1d / (reference * reference) : reference - wavelength;

    public static readonly TheoryData<double, float, float, float, float> CieWeights = new()
    {
        { 450d, 0.14758f, 0f, 1.88447f, 0.05f },
        { 470d, 0f, 0.03483f, 1.35363f, 0.03f },
        { 480d, 0f, 0.20189f, 0.83643f, 0.03f },
        { 490d, 0f, 0.37855f, 0.45114f, 0.03f },
        { 500d, 0f, 0.61251f, 0.22194f, 0.03f },
        { 520d, 0f, 1.27389f, 0f, 0.03f },
        { 550d, 0f, 1.44677f, 0f, 0.03f },
        { 600d, 2.47168f, 0.15424f, 0f, 0.04f },
        { 650d, 0.75419f, 0f, 0f, 0.03f },
    };

    [Theory]
    [MemberData(nameof(CieWeights))]
    public void TheWeightsFollowTheCieColorMatchingFunctions(double wavelength, float red, float green, float blue, float tolerance)
    {
        var weights = Spectrum.Weights(wavelength);

        Assert.InRange(weights.X, red - tolerance, red + tolerance);
        Assert.InRange(weights.Y, green - tolerance, green + tolerance);
        Assert.InRange(weights.Z, blue - tolerance, blue + tolerance);
    }

    public static readonly TheoryData<double, double, double, double> CieTable = new()
    {
        { 400d, 0.014310, 0.000396, 0.067850 },
        { 410d, 0.043510, 0.001210, 0.207400 },
        { 420d, 0.134380, 0.004000, 0.645600 },
        { 430d, 0.283900, 0.011600, 1.385600 },
        { 440d, 0.348280, 0.023000, 1.747060 },
        { 450d, 0.336200, 0.038000, 1.772110 },
        { 460d, 0.290800, 0.060000, 1.669200 },
        { 470d, 0.195360, 0.090980, 1.287640 },
        { 480d, 0.095640, 0.139020, 0.812950 },
        { 490d, 0.032010, 0.208020, 0.465180 },
        { 500d, 0.004900, 0.323000, 0.272000 },
        { 510d, 0.009300, 0.503000, 0.158200 },
        { 520d, 0.063270, 0.710000, 0.078250 },
        { 530d, 0.165500, 0.862000, 0.042160 },
        { 540d, 0.290400, 0.954000, 0.020300 },
        { 550d, 0.433450, 0.994950, 0.008750 },
        { 560d, 0.594500, 0.995000, 0.003900 },
        { 570d, 0.762100, 0.952000, 0.002100 },
        { 580d, 0.916300, 0.870000, 0.001650 },
        { 590d, 1.026300, 0.757000, 0.001100 },
        { 600d, 1.062200, 0.631000, 0.000800 },
        { 610d, 1.002600, 0.503000, 0.000340 },
        { 620d, 0.854450, 0.381000, 0.000190 },
        { 630d, 0.642400, 0.265000, 0.000050 },
        { 640d, 0.447900, 0.175000, 0.000020 },
        { 650d, 0.283500, 0.107000, 0.000000 },
        { 660d, 0.164900, 0.061000, 0.000000 },
        { 670d, 0.087400, 0.032000, 0.000000 },
        { 680d, 0.046770, 0.017000, 0.000000 },
        { 690d, 0.022700, 0.008210, 0.000000 },
        { 700d, 0.011359, 0.004102, 0.000000 },
    };

    [Theory]
    [MemberData(nameof(CieTable))]
    public void TheColorMatchingFunctionsStayWithinOnePointFourPercentOfTheCieTable(double wavelength, double x, double y, double z)
    {
        var fit = Spectrum.ColorMatching(wavelength);

        Assert.InRange(fit.X, x - 0.015, x + 0.015);
        Assert.InRange(fit.Y, y - 0.014, y + 0.014);
        Assert.InRange(fit.Z, z - 0.025, z + 0.025);
    }

    [Fact]
    public void TheWeightsAreNeverNegative()
    {
        for (var wavelength = 380d; wavelength <= 780d; wavelength += 0.5d)
        {
            var weights = Spectrum.Weights(wavelength);

            Assert.True(weights.X >= 0f && weights.Y >= 0f && weights.Z >= 0f, $"{wavelength} nm");
        }
    }

    [Theory]
    [InlineData(0, 600d, 612d)]
    [InlineData(1, 534d, 544d)]
    [InlineData(2, 440d, 452d)]
    public void EachChannelPeaksInItsOwnPartOfTheSpectrum(int channel, double from, double to)
    {
        var peak = Enumerable.Range(400, 301)
            .Select(wavelength => (Wavelength: wavelength, Weights: Spectrum.Weights(wavelength)))
            .MaxBy(item => channel switch { 0 => item.Weights.X, 1 => item.Weights.Y, _ => item.Weights.Z });

        Assert.InRange(peak.Wavelength, from, to);
    }

    [Theory]
    [InlineData(500d, 0)]
    [InlineData(530d, 0)]
    [InlineData(440d, 1)]
    [InlineData(650d, 1)]
    [InlineData(560d, 2)]
    [InlineData(690d, 2)]
    public void AChannelHasNoWeightWhereItsPrimaryIsOutOfGamut(double wavelength, int channel)
    {
        var weights = Spectrum.Weights(wavelength);

        Assert.Equal(0f, channel switch { 0 => weights.X, 1 => weights.Y, _ => weights.Z });
    }

    [Theory]
    [InlineData(8, 550f, DispersionLaw.Grating)]
    [InlineData(32, 550f, DispersionLaw.Grating)]
    [InlineData(64, 550f, DispersionLaw.Grating)]
    [InlineData(13, 400f, DispersionLaw.Prism)]
    [InlineData(40, 700f, DispersionLaw.Prism)]
    [InlineData(25, 620f, DispersionLaw.Prism)]
    public void EachChannelOfTheTableSumsToOne(int steps, float reference, DispersionLaw law)
    {
        var table = Table(steps, reference, law);

        var sums = table.Take(steps).Aggregate(Vector3.Zero, (sum, entry) => sum + new Vector3(entry.X, entry.Y, entry.Z));

        Assert.InRange(sums.X, 0.99999f, 1.00001f);
        Assert.InRange(sums.Y, 0.99999f, 1.00001f);
        Assert.InRange(sums.Z, 0.99999f, 1.00001f);
    }

    [Fact]
    public void EveryStepCountFromEightToSixtyFourSumsToOne()
    {
        for (var steps = Spectrum.MinSteps; steps <= Spectrum.MaxSteps; steps++)
        {
            var table = Table(steps, 550f, DispersionLaw.Grating);
            var sums = table.Take(steps).Aggregate(Vector3.Zero, (sum, entry) => sum + new Vector3(entry.X, entry.Y, entry.Z));

            Assert.True(Math.Abs(sums.X - 1f) < 1e-5f && Math.Abs(sums.Y - 1f) < 1e-5f && Math.Abs(sums.Z - 1f) < 1e-5f, $"{steps} steps");
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(63)]
    public void EntriesBeyondTheStepsAreCleared(int steps)
    {
        var table = Enumerable.Repeat(new Vector4(9f), Spectrum.MaxSteps).ToArray();

        Spectrum.Fill(table, steps, 550f, DispersionLaw.Grating);

        Assert.All(table.Skip(steps), entry => Assert.Equal(Vector4.Zero, entry));
        Assert.All(table.Take(steps), entry => Assert.NotEqual(new Vector4(9f), entry));
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
            Assert.Equal(Offset(law, Wavelength(index, steps), reference) / norm, table[index].W, 1e-5);
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

        Assert.Equal(shortEnd, table[0].W, 1e-5);
        Assert.Equal(longEnd, table[15].W, 1e-5);
    }

    [Theory]
    [InlineData(DispersionLaw.Grating)]
    [InlineData(DispersionLaw.Prism)]
    public void TheShiftCoefficientFallsAsTheWavelengthGrows(DispersionLaw law)
    {
        var table = Table(Spectrum.MaxSteps, 550f, law);

        for (var index = 1; index < Spectrum.MaxSteps; index++)
            Assert.True(table[index].W < table[index - 1].W, $"{index}");
    }

    [Theory]
    [InlineData(DispersionLaw.Grating)]
    [InlineData(DispersionLaw.Prism)]
    public void TheSampleAtTheReferenceWavelengthDoesNotMove(DispersionLaw law)
    {
        var table = Table(13, 550f, law);

        Assert.Equal(0f, table[6].W);
        Assert.NotEqual(0f, table[5].W);
        Assert.NotEqual(0f, table[7].W);
    }

    [Fact]
    public void ThePrismLawSpreadsBlueMoreThanRed()
    {
        var table = Table(16, 550f, DispersionLaw.Prism);

        Assert.InRange(-table[15].W, 0.40f, 0.46f);
        Assert.True(table[0].W > -table[15].W * 2f);
    }

    [Fact]
    public void TheTwoLawsGiveDifferentCoefficients()
    {
        var grating = Table(24, 550f, DispersionLaw.Grating);
        var prism = Table(24, 550f, DispersionLaw.Prism);

        Assert.NotEqual(grating[5].W, prism[5].W);
        Assert.NotEqual(grating[18].W, prism[18].W);
    }

    [Fact]
    public void TheWeightsOfEachChannelCentreOnItsOwnWavelength()
    {
        var table = Table(32, 550f, DispersionLaw.Grating);

        Assert.InRange(Centroid(table, 32, entry => entry.X), 599d, 605d);
        Assert.InRange(Centroid(table, 32, entry => entry.Y), 538d, 544d);
        Assert.InRange(Centroid(table, 32, entry => entry.Z), 449d, 455d);
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
