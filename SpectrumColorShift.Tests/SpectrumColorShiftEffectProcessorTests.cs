using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;

namespace SpectrumColorShift.Tests;

[Collection("Direct2D")]
public sealed class SpectrumColorShiftEffectProcessorTests
{
    const int Width = 64;
    const int Height = 48;
    const int Length = 30;

    static Bgra Textured(int x, int y)
    {
        if ((x / 8 + y / 8) % 5 == 4)
            return Bgra.Transparent;

        return new Bgra((byte)(40 + x * 3 % 200), (byte)(30 + y * 5 % 210), (byte)(255 - (x + y) * 2 % 220), (byte)(110 + (x * 7 + y * 11) % 146));
    }

    static SpectrumColorShiftEffect Shifted(double distance, Action<SpectrumColorShiftEffect>? configure = null)
    {
        var effect = new SpectrumColorShiftEffect();
        effect.Distance.Values[0].Value = distance;
        configure?.Invoke(effect);
        return effect;
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    [Fact]
    public void TheProcessorHandsTheDrawDescriptionBackUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        using var processor = Shifted(8d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Same(description.DrawDescription, draw);
    }

    [Fact]
    public void WithoutADistanceTheProcessorPassesTheImageThrough()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        using var processor = Shifted(0d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void TheProcessorShiftsTheColorsOfTheImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        using var processor = Shifted(8d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.True(rendering.Left < 0);
        Assert.Contains(rendering.Coordinates(), point => source.Contains(point.X, point.Y) && rendering[point.X, point.Y] != source[point.X, point.Y]);
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = Shifted(0d);
        effect.Distance.CopyFrom(Linear(0d, 24d));
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 12);
        RenderFrame(context, processor, 25);
        var again = RenderFrame(context, processor, 12);

        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void AnimatedDistanceIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = Shifted(0d);
        effect.Distance.CopyFrom(Linear(0d, 24d));
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.Equal((0, 0, Width, Height), (start.Left, start.Top, start.Width, start.Height));
        Assert.True(end.Left < -10);
    }

    public static readonly TheoryData<string, ShiftMode, double, double> AnimatedParameters = new()
    {
        { nameof(SpectrumColorShiftEffect.Angle), ShiftMode.Linear, 0d, 90d },
        { nameof(SpectrumColorShiftEffect.CenterX), ShiftMode.Radial, 0d, 60d },
        { nameof(SpectrumColorShiftEffect.CenterY), ShiftMode.Rotational, 0d, -60d },
        { nameof(SpectrumColorShiftEffect.Strength), ShiftMode.Linear, 100d, 20d },
        { nameof(SpectrumColorShiftEffect.ReferenceWavelength), ShiftMode.Linear, 450d, 650d },
    };

    [Theory]
    [MemberData(nameof(AnimatedParameters))]
    public void EveryAnimatedParameterIsReadAtEachFrame(string name, ShiftMode mode, double from, double to)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = Shifted(12d, shifted => shifted.Mode = mode);
        ((Animation)typeof(SpectrumColorShiftEffect).GetProperty(name)!.GetValue(effect)!).CopyFrom(Linear(from, to));
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.False(start.SamePixelsAs(end), name);
    }

    [Fact]
    public void TheProcessorFeedsTheEffectExactlyTheParameters()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = new SpectrumColorShiftEffect { Mode = ShiftMode.Rotational, Dispersion = DispersionLaw.Prism, Steps = 20 };
        effect.Distance.Values[0].Value = 14.5d;
        effect.Angle.Values[0].Value = 33d;
        effect.CenterX.Values[0].Value = 11d;
        effect.CenterY.Values[0].Value = -7d;
        effect.Strength.Values[0].Value = 70d;
        effect.ReferenceWavelength.Values[0].Value = 500d;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        using var direct = new SpectrumColorShiftCustomEffect(context);
        direct.SetInput(0, source.Bitmap, true);
        direct.Distance = 14.5f;
        direct.Mode = (int)ShiftMode.Rotational;
        direct.Angle = 33f;
        direct.CenterX = 11f;
        direct.CenterY = -7f;
        direct.Strength = 0.7f;
        direct.Dispersion = (int)DispersionLaw.Prism;
        direct.ReferenceWavelength = 500f;
        direct.Steps = 20;
        using var expected = direct.Output;

        var rendering = RenderFrame(context, processor, 9);

        Assert.True(rendering.SamePixelsAs(Rendering.Capture(context, expected)));
    }

    public static readonly TheoryData<string, Action<SpectrumColorShiftEffect>, Action<SpectrumColorShiftEffect>> LaterChanges = new()
    {
        { nameof(SpectrumColorShiftEffect.Distance), effect => effect.Distance.Values[0].Value = 4d, effect => effect.Distance.Values[0].Value = 16d },
        { nameof(SpectrumColorShiftEffect.Mode), effect => { }, effect => effect.Mode = ShiftMode.Radial },
        { nameof(SpectrumColorShiftEffect.Angle), effect => { }, effect => effect.Angle.Values[0].Value = 90d },
        { nameof(SpectrumColorShiftEffect.CenterX), effect => effect.Mode = ShiftMode.Radial, effect => effect.CenterX.Values[0].Value = 20d },
        { nameof(SpectrumColorShiftEffect.CenterY), effect => effect.Mode = ShiftMode.Rotational, effect => effect.CenterY.Values[0].Value = -15d },
        { nameof(SpectrumColorShiftEffect.Strength), effect => { }, effect => effect.Strength.Values[0].Value = 40d },
        { nameof(SpectrumColorShiftEffect.Dispersion), effect => { }, effect => effect.Dispersion = DispersionLaw.Prism },
        { nameof(SpectrumColorShiftEffect.ReferenceWavelength), effect => { }, effect => effect.ReferenceWavelength.Values[0].Value = 450d },
        { nameof(SpectrumColorShiftEffect.Steps), effect => { }, effect => effect.Steps = 8 },
    };

    [Theory]
    [MemberData(nameof(LaterChanges))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheEffect(string setting, Action<SpectrumColorShiftEffect> start, Action<SpectrumColorShiftEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = Shifted(8d, start);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 5);
        change(effect);
        var after = RenderFrame(context, processor, 5);

        Assert.False(before.SamePixelsAs(after), setting);
    }

    [Theory]
    [MemberData(nameof(LaterChanges))]
    public void AProcessorThatDrewAnotherSettingDrawsLikeAFreshOne(string setting, Action<SpectrumColorShiftEffect> start, Action<SpectrumColorShiftEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = Shifted(8d, start);
        using var used = effect.CreateVideoEffect(context);
        used.SetInput(source.Bitmap);
        RenderFrame(context, used, 5);
        change(effect);
        using var fresh = effect.CreateVideoEffect(context);
        fresh.SetInput(source.Bitmap);

        var after = RenderFrame(context, used, 5);
        var expected = RenderFrame(context, fresh, 5);

        Assert.True(after.SamePixelsAs(expected), setting);
    }

    [Fact]
    public void ChangingTheDistanceToZeroLaterPassesTheImageThrough()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Textured);
        var effect = Shifted(8d);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var shifted = RenderFrame(context, processor, 0);
        effect.Distance.Values[0].Value = 0d;
        var still = RenderFrame(context, processor, 0);

        Assert.False(shifted.SamePixelsAs(still));
        Assert.Equal((0, 0, Width, Height), (still.Left, still.Top, still.Width, still.Height));
        Assert.All(still.Coordinates(), point => Assert.Equal(source[point.X, point.Y], still[point.X, point.Y]));
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Shifted(8d).CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    [Fact]
    public void AFailureWhileCreatingTheProcessorIsNotSwallowed()
    {
        var effect = Shifted(8d);

        Assert.ThrowsAny<Exception>(() => effect.CreateVideoEffect(null!));
    }
}
