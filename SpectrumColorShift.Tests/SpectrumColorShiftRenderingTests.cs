using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift.Tests;

[Collection("Direct2D")]
public sealed class SpectrumColorShiftRenderingTests
{
    const int Seam = 32;

    static Bgra HalfAndHalf(int x, int y) => x < Seam ? Bgra.Opaque(255, 255, 255) : Bgra.Opaque(0, 0, 0);

    static Bgra TopAndBottom(int x, int y) => y < Seam ? Bgra.Opaque(255, 255, 255) : Bgra.Opaque(0, 0, 0);

    static Bgra Stripes(int x, int y) => Math.Abs(x + 0.5 - 64) is >= 30 and < 34 ? Bgra.Opaque(255, 255, 255) : Bgra.Opaque(0, 0, 0);

    static Bgra Dot(int x, int y) => Math.Abs(x + 0.5 - 104) < 3 && Math.Abs(y + 0.5 - 64) < 3 ? Bgra.Opaque(255, 255, 255) : Bgra.Opaque(0, 0, 0);

    static Bgra Textured(int x, int y)
    {
        if ((x / 8 + y / 8) % 5 == 4)
            return Bgra.Transparent;

        return new Bgra((byte)(40 + x * 3 % 200), (byte)(30 + y * 5 % 210), (byte)(255 - (x + y) * 2 % 220), (byte)(110 + (x * 7 + y * 11) % 146));
    }

    static Bgra PointSymmetric(int x, int y, int mirroredX, int mirroredY)
    {
        var value = Math.Min(x * 3 + y * 5, mirroredX * 3 + mirroredY * 5);

        return Bgra.Opaque((byte)(40 + value * 7 % 200), (byte)(30 + value * 11 % 210), (byte)(255 - value * 13 % 220));
    }

    static Rendering Render(IGraphicsDevicesAndContext devices, SourceImage source, ShiftSettings settings)
    {
        using var effect = new SpectrumColorShiftCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        settings.Apply(effect);
        using var output = effect.Output;

        return Rendering.Capture(devices, output);
    }

    static bool WithinRounding(Bgra expected, Bgra actual, int tolerance = 1)
        => Math.Abs(expected.Blue - actual.Blue) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance && Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Alpha - actual.Alpha) <= tolerance;

    static double DistanceToBorder(SourceImage source, int x, int y)
        => Math.Min(Math.Min(x, source.Width - 1 - x), Math.Min(y, source.Height - 1 - y));

    static double Crossing(Func<int, byte> value, int from, int to)
    {
        for (var index = from; index < to; index++)
        {
            double a = value(index);
            double b = value(index + 1);
            if (a >= 127.5 && b < 127.5)
                return index + 0.5 + (a - 127.5) / (a - b);
        }

        return double.NaN;
    }

    static double EdgeWidth(Func<int, byte> value, int from, int to)
    {
        double high = double.NaN;
        double low = double.NaN;
        for (var index = from; index < to; index++)
        {
            double a = value(index);
            double b = value(index + 1);
            if (double.IsNaN(high) && a >= 230 && b < 230)
                high = index + 0.5 + (a - 230) / (a - b);
            if (double.IsNaN(low) && a >= 25 && b < 25)
                low = index + 0.5 + (a - 25) / (a - b);
        }

        return low - high;
    }

    static double MeanCoefficient(ShiftSettings settings, int channel)
    {
        var table = new Vector4[Spectrum.MaxSteps];
        Spectrum.Fill(table, settings.Steps, settings.ReferenceWavelength, settings.Dispersion);

        return table.Take(Spectrum.ClampSteps(settings.Steps)).Sum(entry => (channel switch { 0 => entry.X, 1 => entry.Y, _ => entry.Z }) * (double)entry.W);
    }

    static Func<int, byte> Channel(Rendering rendering, int channel, Func<int, (int X, int Y)> position)
        => index =>
        {
            var (x, y) = position(index);
            var pixel = rendering[x, y];

            return channel switch { 0 => pixel.Red, 1 => pixel.Green, _ => pixel.Blue };
        };

    static int PeakAt(Rendering rendering, int channel, int from, int to, Func<int, (int X, int Y)> position)
    {
        var value = Channel(rendering, channel, position);

        return Enumerable.Range(from, to - from + 1).MaxBy(index => value(index));
    }

    public static readonly TheoryData<ShiftMode> Modes = new() { ShiftMode.Linear, ShiftMode.Radial, ShiftMode.Rotational };

    [Theory]
    [MemberData(nameof(Modes))]
    public void WithoutADistanceTheImagePassesThroughUntouched(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 0f, Center: new Vector2(9f, -5f)));

        Assert.Equal((0, 0, source.Width, source.Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void WithoutStrengthTheImageIsUntouchedAndTheSpillStaysTransparent(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 12f, Strength: 0f));

        Assert.True(rendering.Left < 0 && rendering.Top < 0);
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source.Contains(point.X, point.Y) ? source[point.X, point.Y] : Bgra.Transparent, rendering[point.X, point.Y]));
    }

    [Theory]
    [InlineData(ShiftMode.Linear, DispersionLaw.Grating, 8)]
    [InlineData(ShiftMode.Linear, DispersionLaw.Prism, 33)]
    [InlineData(ShiftMode.Radial, DispersionLaw.Grating, 32)]
    [InlineData(ShiftMode.Radial, DispersionLaw.Prism, 64)]
    [InlineData(ShiftMode.Rotational, DispersionLaw.Grating, 17)]
    [InlineData(ShiftMode.Rotational, DispersionLaw.Prism, 32)]
    public void AUniformImageKeepsItsColorInTheInterior(ShiftMode mode, DispersionLaw law, int steps)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var color = new Bgra(200, 90, 30, 255);
        using var source = SourceImage.Solid(context, 96, 96, color);

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 12f, Dispersion: law, Steps: steps, ReferenceWavelength: 600f));

        var checkedPixels = 0;
        foreach (var (x, y) in rendering.Coordinates())
        {
            if (source.Contains(x, y) && DistanceToBorder(source, x, y) > 16)
            {
                Assert.True(WithinRounding(color, rendering[x, y]), $"({x}, {y}) {rendering[x, y]}");
                checkedPixels++;
            }
        }

        Assert.True(checkedPixels > 3000, $"{checkedPixels}");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ASemiTransparentUniformImageKeepsItsPremultipliedColor(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var color = new Bgra(200, 90, 30, 128);
        using var source = SourceImage.Solid(context, 96, 96, color);

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 12f));

        foreach (var (x, y) in rendering.Coordinates())
        {
            if (source.Contains(x, y) && DistanceToBorder(source, x, y) > 16)
                Assert.True(WithinRounding(color.Premultiplied(), rendering[x, y]), $"({x}, {y}) {rendering[x, y]}");
        }
    }

    [Theory]
    [InlineData(8f, 0f, DispersionLaw.Grating, 550f, 32)]
    [InlineData(8f, 0f, DispersionLaw.Prism, 550f, 32)]
    [InlineData(8f, 0f, DispersionLaw.Grating, 450f, 32)]
    [InlineData(8f, 0f, DispersionLaw.Grating, 650f, 32)]
    [InlineData(8f, 180f, DispersionLaw.Grating, 550f, 32)]
    [InlineData(-8f, 0f, DispersionLaw.Grating, 550f, 32)]
    [InlineData(16f, 0f, DispersionLaw.Grating, 550f, 64)]
    [InlineData(6f, 0f, DispersionLaw.Prism, 500f, 16)]
    public void TheEdgeOfEachChannelMovesByItsMeanShiftCoefficientAlongTheAngle(float distance, float angle, DispersionLaw law, float reference, int steps)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 32, HalfAndHalf);
        var settings = new ShiftSettings(Distance: distance, Angle: angle, Dispersion: law, ReferenceWavelength: reference, Steps: steps);
        var direction = MathF.Cos(angle * MathF.PI / 180f);

        var rendering = Render(context, source, settings);

        for (var channel = 0; channel < 3; channel++)
        {
            var crossing = Crossing(Channel(rendering, channel, x => (x, 16)), 4, 58);
            var expected = Seam + distance * direction * MeanCoefficient(settings, channel);
            Assert.True(Math.Abs(crossing - expected) <= 0.3 + 0.04 * Math.Abs(distance), $"channel {channel}: {crossing} against {expected}");
        }
    }

    [Theory]
    [InlineData(90f)]
    [InlineData(-90f)]
    public void AVerticalAngleMovesTheEdgesOfAHorizontalSeam(float angle)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 32, 64, TopAndBottom);
        var settings = new ShiftSettings(Distance: 8f, Angle: angle);
        var direction = MathF.Sin(angle * MathF.PI / 180f);

        var rendering = Render(context, source, settings);

        for (var channel = 0; channel < 3; channel++)
        {
            var crossing = Crossing(Channel(rendering, channel, y => (16, y)), 4, 58);
            var expected = Seam + 8f * direction * MeanCoefficient(settings, channel);
            Assert.True(Math.Abs(crossing - expected) <= 0.7, $"channel {channel}: {crossing} against {expected}");
        }
    }

    [Fact]
    public void BlueMovesAheadOfGreenAndRedAlongTheDirection()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 32, HalfAndHalf);

        var rendering = Render(context, source, new ShiftSettings());
        var red = Crossing(Channel(rendering, 0, x => (x, 16)), 4, 58);
        var green = Crossing(Channel(rendering, 1, x => (x, 16)), 4, 58);
        var blue = Crossing(Channel(rendering, 2, x => (x, 16)), 4, 58);

        Assert.InRange(red, 27d, 30.5d);
        Assert.InRange(green, 31.5d, 33.5d);
        Assert.InRange(blue, 36d, 38.5d);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(37f)]
    [InlineData(90f)]
    [InlineData(200f)]
    public void ANegativeDistanceMirrorsAPositiveOneAtTheOppositeAngle(float angle)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);

        var negative = Render(context, source, new ShiftSettings(Distance: -10f, Angle: angle));
        var opposite = Render(context, source, new ShiftSettings(Distance: 10f, Angle: angle + 180f));

        Assert.Equal((negative.Left, negative.Top, negative.Width, negative.Height), (opposite.Left, opposite.Top, opposite.Width, opposite.Height));
        Assert.All(negative.Coordinates(), point => Assert.True(WithinRounding(negative[point.X, point.Y], opposite[point.X, point.Y]), $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void ThePrismLawNarrowsTheRedEdgeComparedWithTheGrating()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 32, HalfAndHalf);

        var grating = Render(context, source, new ShiftSettings(Dispersion: DispersionLaw.Grating));
        var prism = Render(context, source, new ShiftSettings(Dispersion: DispersionLaw.Prism));
        var gratingWidth = EdgeWidth(Channel(grating, 0, x => (x, 16)), 4, 58);
        var prismWidth = EdgeWidth(Channel(prism, 0, x => (x, 16)), 4, 58);

        Assert.True(prismWidth < gratingWidth * 0.6, $"prism {prismWidth}, grating {gratingWidth}");
    }

    [Fact]
    public void MoreStepsConvergeTowardsTheDenseSpectrum()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);
        var dense = Render(context, source, new ShiftSettings(Distance: 24f, Steps: 64));

        long Distance(int steps)
        {
            var coarse = Render(context, source, new ShiftSettings(Distance: 24f, Steps: steps));
            return dense.Coordinates().Sum(point =>
            {
                var a = dense[point.X, point.Y];
                var b = coarse[point.X, point.Y];
                return (long)(Math.Abs(a.Blue - b.Blue) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Red - b.Red) + Math.Abs(a.Alpha - b.Alpha));
            });
        }

        var eight = Distance(8);
        var sixteen = Distance(16);
        var thirtyTwo = Distance(32);

        Assert.True(eight > sixteen, $"8: {eight}, 16: {sixteen}");
        Assert.True(sixteen > thirtyTwo, $"16: {sixteen}, 32: {thirtyTwo}");
    }

    static readonly ShiftSettings[] ReferenceSettings =
    [
        new ShiftSettings(),
        new ShiftSettings(Distance: 20f, Angle: 37f),
        new ShiftSettings(Distance: -14f, Angle: 215f, Dispersion: DispersionLaw.Prism),
        new ShiftSettings(Mode: ShiftMode.Radial, Distance: 20f),
        new ShiftSettings(Mode: ShiftMode.Radial, Distance: -18f, Center: new Vector2(30f, -12f)),
        new ShiftSettings(Mode: ShiftMode.Rotational, Distance: 20f, Center: new Vector2(12f, -9f)),
        new ShiftSettings(Mode: ShiftMode.Rotational, Distance: 35f),
        new ShiftSettings(Mode: ShiftMode.Radial, Distance: 60f),
        new ShiftSettings(Mode: ShiftMode.Radial, Distance: -75f, Center: new Vector2(-25f, 10f), Steps: 16),
        new ShiftSettings(Mode: ShiftMode.Rotational, Distance: 90f, Center: new Vector2(20f, 14f), Steps: 64),
        new ShiftSettings(Distance: 14f, Dispersion: DispersionLaw.Prism, ReferenceWavelength: 480f, Steps: 8, Strength: 0.6f),
        new ShiftSettings(Distance: 40f, Angle: 200f, Dispersion: DispersionLaw.Prism, ReferenceWavelength: 650f, Steps: 64),
    ];

    public static readonly TheoryData<int> ReferenceCases = [.. Enumerable.Range(0, ReferenceSettings.Length)];

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void TheOutputMatchesAnIndependentCpuRendering(int index)
    {
        var settings = ReferenceSettings[index];
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);
        var reference = new ShiftReferenceRenderer(source, settings);

        var rendering = Render(context, source, settings);

        foreach (var (x, y) in rendering.Coordinates())
        {
            var expected = reference.Pixel(x, y) * 255f;
            var actual = rendering[x, y];
            Assert.True(
                Math.Abs(expected.X - actual.Red) <= 2f && Math.Abs(expected.Y - actual.Green) <= 2f && Math.Abs(expected.Z - actual.Blue) <= 2f && Math.Abs(expected.W - actual.Alpha) <= 2f,
                $"({x}, {y}) expected {expected}, actual {actual}");
        }
    }

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void NothingVisibleLiesBeyondTheOutputBounds(int index)
    {
        var settings = ReferenceSettings[index];
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);
        var reference = new ShiftReferenceRenderer(source, settings);

        var rendering = Render(context, source, settings);

        for (var y = rendering.Top - 8; y < rendering.Bottom + 8; y++)
        {
            for (var x = rendering.Left - 8; x < rendering.Right + 8; x++)
            {
                if (!rendering.Contains(x, y))
                    Assert.True(reference.Pixel(x, y).W * 255f <= 1f, $"({x}, {y}) {reference.Pixel(x, y)}");
            }
        }
    }

    [Fact]
    public void BlueSpillsOutwardAndRedInwardOfARadialStripe()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 128, 128, Stripes);

        var rendering = Render(context, source, new ShiftSettings(Mode: ShiftMode.Radial, Distance: 20f));
        var red = PeakAt(rendering, 0, 80, 120, x => (x, 64));
        var green = PeakAt(rendering, 1, 80, 120, x => (x, 64));
        var blue = PeakAt(rendering, 2, 80, 120, x => (x, 64));

        Assert.True(red < 94 && green is >= 94 and <= 97 && blue > 98, $"red {red}, green {green}, blue {blue}");
    }

    [Fact]
    public void ARadialShiftKeepsTheMirrorSymmetryOfTheImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 128, 128, Stripes);

        var rendering = Render(context, source, new ShiftSettings(Mode: ShiftMode.Radial, Distance: 20f));

        Assert.Equal(rendering.Left + rendering.Right, source.Width);
        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(rendering[point.X, point.Y], rendering[source.Width - 1 - point.X, point.Y]), $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void BlueTurnsClockwiseAndRedCounterclockwiseAroundTheCenter()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 160, 128, Dot);

        var rendering = Render(context, source, new ShiftSettings(Mode: ShiftMode.Rotational, Distance: 40f));
        var red = PeakAt(rendering, 0, 40, 90, y => (104, y));
        var green = PeakAt(rendering, 1, 40, 90, y => (104, y));
        var blue = PeakAt(rendering, 2, 40, 90, y => (104, y));

        Assert.True(red < 62 && green is >= 62 and <= 66 && blue > 66, $"red {red}, green {green}, blue {blue}");
    }

    [Theory]
    [InlineData(ShiftMode.Radial)]
    [InlineData(ShiftMode.Rotational)]
    public void APointSymmetricImageStaysPointSymmetric(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 128, 128, (x, y) => PointSymmetric(x, y, 127 - x, 127 - y));

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 20f));

        Assert.Equal((rendering.Left + rendering.Right, rendering.Top + rendering.Bottom), (source.Width, source.Height));
        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(rendering[point.X, point.Y], rendering[source.Width - 1 - point.X, source.Height - 1 - point.Y]), $"({point.X}, {point.Y})"));
    }

    [Theory]
    [InlineData(ShiftMode.Radial)]
    [InlineData(ShiftMode.Rotational)]
    public void TheCenterOffsetMovesThePointOfSymmetry(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 128, 128, (x, y) => PointSymmetric(x, y, 167 - x, 107 - y));

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 20f, Center: new Vector2(20f, -10f)));

        var compared = 0;
        foreach (var (x, y) in rendering.Coordinates())
        {
            var mirroredX = 167 - x;
            var mirroredY = 107 - y;
            if (DistanceToBorder(source, x, y) > 24 && source.Contains(mirroredX, mirroredY) && DistanceToBorder(source, mirroredX, mirroredY) > 24)
            {
                Assert.True(WithinRounding(rendering[x, y], rendering[mirroredX, mirroredY]), $"({x}, {y})");
                compared++;
            }
        }

        Assert.True(compared > 1000, $"{compared}");
    }

    [Theory]
    [InlineData(ShiftMode.Linear, 100, 50)]
    [InlineData(ShiftMode.Linear, -37, 21)]
    [InlineData(ShiftMode.Radial, 100, 50)]
    [InlineData(ShiftMode.Radial, -37, 21)]
    [InlineData(ShiftMode.Rotational, 100, 50)]
    [InlineData(ShiftMode.Rotational, -37, 21)]
    public void TheShiftTravelsWithTheImage(ShiftMode mode, int dx, int dy)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);
        using var moved = new AffineTransform2D(context.DeviceContext)
        {
            InterPolationMode = AffineTransform2DInterpolationMode.NearestNeighbor,
            BorderMode = BorderMode.Hard,
            TransformMatrix = Matrix3x2.CreateTranslation(dx, dy),
        };
        moved.SetInput(0, source.Bitmap, true);
        using var movedOutput = moved.Output;
        var settings = new ShiftSettings(Mode: mode, Distance: 16f, Angle: 30f, Center: new Vector2(6f, -4f));

        var inPlace = Render(context, source, settings);
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetInput(0, movedOutput, true);
        settings.Apply(effect);
        using var output = effect.Output;
        var travelled = Rendering.Capture(context, output);

        Assert.Equal((inPlace.Left + dx, inPlace.Top + dy, inPlace.Width, inPlace.Height), (travelled.Left, travelled.Top, travelled.Width, travelled.Height));
        Assert.All(inPlace.Coordinates(), point => Assert.True(WithinRounding(inPlace[point.X, point.Y], travelled[point.X + dx, point.Y + dy]), $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void TheSpectrumSpillsOverTheEdgesInTheDirectionOfTheShift()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(255, 255, 255));

        var rendering = Render(context, source, new ShiftSettings(Distance: 20f));

        var left = rendering[-5, 12];
        var right = rendering[44, 12];
        Assert.True(left.Alpha > 100 && left.Red > left.Blue + 100, $"{left}");
        Assert.True(right.Alpha > 100 && right.Blue > right.Red + 100, $"{right}");
        Assert.True(rendering[rendering.Left, 12].Alpha <= 2 && rendering[rendering.Right - 1, 12].Alpha <= 2);
        Assert.Equal(Bgra.Transparent, rendering[20, rendering.Top]);
        Assert.Equal(Bgra.Transparent, rendering[20, rendering.Bottom - 1]);
    }

    [Fact]
    public void PixelsBeyondTheReachOfTheDistanceStayUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 32, HalfAndHalf);

        var rendering = Render(context, source, new ShiftSettings(Distance: 8f));

        var checkedPixels = 0;
        foreach (var (x, y) in rendering.Coordinates())
        {
            if (source.Contains(x, y) && Math.Abs(x + 0.5 - Seam) > 9 && DistanceToBorder(source, x, y) > 9 && y > 9 && y < 22)
            {
                Assert.True(WithinRounding(source[x, y], rendering[x, y]), $"({x}, {y})");
                checkedPixels++;
            }
        }

        Assert.True(checkedPixels > 100, $"{checkedPixels}");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ATransparentImageStaysTransparent(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 48, 32, Bgra.Transparent);

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 20f));

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryPixelIsAValidPremultipliedColor(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);

        var rendering = Render(context, source, new ShiftSettings(Mode: mode, Distance: 24f, Strength: 0.8f));

        Assert.All(rendering.Coordinates(), point =>
        {
            var pixel = rendering[point.X, point.Y];
            Assert.True(pixel.Red <= pixel.Alpha + 1 && pixel.Green <= pixel.Alpha + 1 && pixel.Blue <= pixel.Alpha + 1, $"({point.X}, {point.Y}) {pixel}");
        });
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void AnOverbrightInputIsClampedToItsAlpha(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var stride = 96 * SourceImage.BytesPerPixel;
        var pixels = new byte[stride * 96];
        for (var offset = 0; offset < pixels.Length; offset += SourceImage.BytesPerPixel)
        {
            pixels[offset + 2] = 255;
            pixels[offset + 3] = 64;
        }

        using var bitmap = context.DeviceContext.CreateBitmap(new SizeI(96, 96), nint.Zero, stride, new BitmapProperties1(SourceImage.PixelFormat, SourceImage.Dpi, SourceImage.Dpi, BitmapOptions.None));
        bitmap.CopyFromMemory(pixels, stride);
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetInput(0, bitmap, true);
        new ShiftSettings(Mode: mode, Distance: 12f).Apply(effect);
        using var output = effect.Output;

        var rendering = Rendering.Capture(context, output);

        var interior = rendering[48, 48];
        Assert.Equal(64, interior.Alpha);
        Assert.True(interior.Red <= interior.Alpha && interior.Green <= interior.Alpha && interior.Blue <= interior.Alpha, $"{interior}");
        Assert.True(interior.Red >= interior.Alpha - 1, $"{interior}");
    }

    [Fact]
    public void TheSameSettingsAlwaysProduceTheSamePixels()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 48, Textured);
        var settings = new ShiftSettings(Mode: ShiftMode.Rotational, Distance: 18f, Dispersion: DispersionLaw.Prism);

        var first = Render(context, source, settings);
        var second = Render(context, source, settings);

        Assert.True(first.SamePixelsAs(second));
    }
}
