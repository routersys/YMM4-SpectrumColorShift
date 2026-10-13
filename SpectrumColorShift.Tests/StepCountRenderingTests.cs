using System.Numerics;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift.Tests;

[Collection("Direct2D")]
public sealed class StepCountRenderingTests
{
    public static readonly TheoryData<ShiftMode, bool> Cases = [.. from physical in new[] { false, true } from mode in new[] { ShiftMode.Linear, ShiftMode.Radial, ShiftMode.Rotational } select (mode, physical)];

    static Bgra Gradient(int x, int y)
    {
        if ((x / 6 + y / 6) % 4 == 3)
            return Bgra.Transparent;

        return new Bgra((byte)(30 + x * 4 % 220), (byte)(20 + y * 7 % 230), (byte)(250 - (x + y) * 3 % 200), (byte)(120 + (x * 5 + y * 9) % 136));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryStepCountMatchesTheCpuRendering(ShiftMode mode, bool physical)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 32, Gradient);
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        using var output = effect.Output;

        foreach (var steps in Enumerable.Range(Spectrum.MinSteps, Spectrum.MaxSteps - Spectrum.MinSteps + 1))
        {
            var settings = new ShiftSettings(Mode: mode, Distance: 16f, Angle: 30f, Steps: steps, Physical: physical);
            settings.Apply(effect);
            Func<int, int, Vector4> reference = physical ? new SpectralReferenceRenderer(source, settings).Pixel : new ShiftReferenceRenderer(source, settings).Pixel;

            var rendering = Rendering.Capture(context, output);

            foreach (var (x, y) in rendering.Coordinates())
            {
                var expected = reference(x, y) * 255f;
                var actual = rendering[x, y];
                Assert.True(
                    Math.Abs(expected.X - actual.Red) <= 2f && Math.Abs(expected.Y - actual.Green) <= 2f && Math.Abs(expected.Z - actual.Blue) <= 2f && Math.Abs(expected.W - actual.Alpha) <= 2f,
                    $"steps {steps}, ({x}, {y}) expected {expected}, actual {actual}");
            }
        }
    }
}
