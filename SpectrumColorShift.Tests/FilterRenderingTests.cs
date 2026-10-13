using System.Numerics;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift.Tests;

[Collection("Direct2D")]
public sealed class FilterRenderingTests
{
    static Bgra Gradient(int x, int y)
    {
        if ((x / 5 + y / 7) % 5 == 4)
            return Bgra.Transparent;

        return new Bgra((byte)(25 + x * 5 % 225), (byte)(15 + y * 9 % 235), (byte)(250 - (x * 2 + y * 3) % 210), (byte)(100 + (x * 3 + y * 5) % 156));
    }

    public static readonly TheoryData<float, float, int, DispersionLaw> Shifts = new()
    {
        { 0f, 8f, 32, DispersionLaw.Grating },
        { 0f, 2.5f, 16, DispersionLaw.Prism },
        { 0f, -9f, 32, DispersionLaw.Grating },
        { 180f, 6f, 24, DispersionLaw.Grating },
        { 90f, 8f, 32, DispersionLaw.Grating },
        { 270f, 5.25f, 20, DispersionLaw.Prism },
        { 0f, 19f, 64, DispersionLaw.Grating },
        { 90f, 0.6f, 12, DispersionLaw.Grating },
        { 0f, 4f, 8, DispersionLaw.Grating },
    };

    [Theory]
    [MemberData(nameof(Shifts))]
    public void AnAxisAlignedShiftMatchesTheCpuRendering(float angle, float distance, int steps, DispersionLaw law)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 40, Gradient);
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetSource(source.Bitmap, true);
        using var output = effect.Output;
        var settings = new ShiftSettings(ShiftMode.Linear, distance, angle, default, 1f, law, 550f, steps, true);
        settings.Apply(effect);
        var reference = new SpectralReferenceRenderer(source, settings);

        var rendering = Rendering.Capture(context, output);

        foreach (var (x, y) in rendering.Coordinates())
        {
            var expected = reference.Pixel(x, y) * 255f;
            var actual = rendering[x, y];
            Assert.True(
                Math.Abs(expected.X - actual.Red) <= 2f && Math.Abs(expected.Y - actual.Green) <= 2f && Math.Abs(expected.Z - actual.Blue) <= 2f && Math.Abs(expected.W - actual.Alpha) <= 2f,
                $"({x}, {y}) expected {expected}, actual {actual}");
        }
    }

    [Fact]
    public void SwitchingBetweenTheFilterAndTheStepwiseShaderKeepsTheReferenceImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 40, Gradient);
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetSource(source.Bitmap, true);
        using var output = effect.Output;

        foreach (var (angle, distance) in new[] { (0f, 8f), (30f, 8f), (0f, 8f), (0f, 40f), (90f, 8f), (0f, 0f), (0f, 8f) })
        {
            var settings = new ShiftSettings(ShiftMode.Linear, distance, angle, default, 1f, DispersionLaw.Grating, 550f, 32, true);
            settings.Apply(effect);
            var reference = new SpectralReferenceRenderer(source, settings);

            var rendering = Rendering.Capture(context, output);

            foreach (var (x, y) in rendering.Coordinates())
            {
                var expected = reference.Pixel(x, y) * 255f;
                var actual = rendering[x, y];
                Assert.True(
                    Math.Abs(expected.X - actual.Red) <= 2f && Math.Abs(expected.Y - actual.Green) <= 2f && Math.Abs(expected.Z - actual.Blue) <= 2f && Math.Abs(expected.W - actual.Alpha) <= 2f,
                    $"angle {angle}, distance {distance}: ({x}, {y}) expected {expected}, actual {actual}");
            }
        }
    }
}
