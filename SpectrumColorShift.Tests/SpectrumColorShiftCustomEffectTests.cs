using System.Numerics;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using static SpectrumColorShift.SpectrumColorShiftCustomEffect;

namespace SpectrumColorShift.Tests;

[Collection("Direct2D")]
public sealed class SpectrumColorShiftCustomEffectTests
{
    const float MaxInputPixel = 4096f;

    static float Read(SpectrumColorShiftCustomEffect effect, PropertyIndex index) => effect.GetFloatValue((int)index);

    static int ReadInt(SpectrumColorShiftCustomEffect effect, PropertyIndex index) => effect.GetIntValue((int)index);

    static (float Left, float Top, float Right, float Bottom) Bounds(IGraphicsDevicesAndContext devices, SourceImage source, Action<SpectrumColorShiftCustomEffect> configure)
    {
        using var effect = new SpectrumColorShiftCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        configure(effect);
        using var output = effect.Output;
        var bounds = devices.DeviceContext.GetImageLocalBounds(output);

        return (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromTheirDefaults()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        Assert.Equal(0f, Read(effect, PropertyIndex.Distance));
        Assert.Equal((int)ShiftMode.Linear, ReadInt(effect, PropertyIndex.Mode));
        Assert.Equal(0f, Read(effect, PropertyIndex.Angle));
        Assert.Equal(0f, Read(effect, PropertyIndex.CenterX));
        Assert.Equal(0f, Read(effect, PropertyIndex.CenterY));
        Assert.Equal(1f, Read(effect, PropertyIndex.Strength));
        Assert.Equal((int)DispersionLaw.Grating, ReadInt(effect, PropertyIndex.Dispersion));
        Assert.Equal(550f, Read(effect, PropertyIndex.ReferenceWavelength));
        Assert.Equal(32, ReadInt(effect, PropertyIndex.Steps));
    }

    [Theory]
    [InlineData(-MaxInputPixel - 1f, -MaxInputPixel)]
    [InlineData(-5f, -5f)]
    [InlineData(0f, 0f)]
    [InlineData(2.5f, 2.5f)]
    [InlineData(MaxInputPixel, MaxInputPixel)]
    [InlineData(MaxInputPixel + 1f, MaxInputPixel)]
    [InlineData(float.MaxValue, MaxInputPixel)]
    [InlineData(float.MinValue, -MaxInputPixel)]
    [InlineData(float.PositiveInfinity, MaxInputPixel)]
    [InlineData(float.NegativeInfinity, -MaxInputPixel)]
    [InlineData(float.NaN, 0f)]
    public void TheDistanceIsClampedToTheInputPixelRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.Distance = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Distance));
    }

    [Theory]
    [InlineData(-MaxInputPixel - 1f, -MaxInputPixel)]
    [InlineData(-30f, -30f)]
    [InlineData(0f, 0f)]
    [InlineData(45.5f, 45.5f)]
    [InlineData(MaxInputPixel + 1f, MaxInputPixel)]
    [InlineData(float.PositiveInfinity, MaxInputPixel)]
    [InlineData(float.NaN, 0f)]
    public void TheCenterOffsetIsClampedToTheInputPixelRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.CenterX = value;
        effect.CenterY = -value;

        Assert.Equal(expected, Read(effect, PropertyIndex.CenterX));
        Assert.Equal(float.IsNaN(value) ? 0f : -expected, Read(effect, PropertyIndex.CenterY));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, 0f)]
    public void TheStrengthIsClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.Strength = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Strength));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(8, 1)]
    [InlineData(-1, 1)]
    [InlineData(int.MaxValue, 1)]
    public void AnUnknownModeFallsBackToTheLinearMode(int value, int expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.Mode = value;

        Assert.Equal(expected, ReadInt(effect, PropertyIndex.Mode));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(-1, 0)]
    [InlineData(int.MaxValue, 0)]
    public void AnUnknownDispersionFallsBackToTheGrating(int value, int expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.Dispersion = value;

        Assert.Equal(expected, ReadInt(effect, PropertyIndex.Dispersion));
    }

    [Theory]
    [InlineData(300f, 400f)]
    [InlineData(400f, 400f)]
    [InlineData(623.5f, 623.5f)]
    [InlineData(700f, 700f)]
    [InlineData(900f, 700f)]
    [InlineData(float.NaN, 550f)]
    public void TheReferenceWavelengthStaysWithinTheSpectrum(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.ReferenceWavelength = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.ReferenceWavelength));
    }

    [Theory]
    [InlineData(int.MinValue, 8)]
    [InlineData(0, 8)]
    [InlineData(8, 8)]
    [InlineData(40, 40)]
    [InlineData(64, 64)]
    [InlineData(100, 64)]
    [InlineData(int.MaxValue, 64)]
    public void TheStepsStayBetweenEightAndSixtyFour(int value, int expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.Steps = value;

        Assert.Equal(expected, ReadInt(effect, PropertyIndex.Steps));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(90f, 90f)]
    [InlineData(-45f, -45f)]
    [InlineData(135f, 135f)]
    [InlineData(-170f, -170f)]
    [InlineData(450f, 90f)]
    [InlineData(-360f, 0f)]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(float.NegativeInfinity, 0f)]
    public void TheAngleIsKeptAsADirectionInDegrees(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new SpectrumColorShiftCustomEffect(context);

        effect.Angle = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Angle), 0.01);
    }

    [Theory]
    [InlineData(ShiftMode.Linear)]
    [InlineData(ShiftMode.Radial)]
    [InlineData(ShiftMode.Rotational)]
    public void WithoutADistanceTheOutputBoundsEqualTheInput(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));

        var bounds = Bounds(context, source, effect =>
        {
            effect.Mode = (int)mode;
            effect.CenterX = 15f;
        });

        Assert.Equal((0f, 0f, 40f, 24f), bounds);
    }

    [Theory]
    [InlineData(10f, 0f, -12f, -2f, 52f, 26f)]
    [InlineData(-10f, 0f, -12f, -2f, 52f, 26f)]
    [InlineData(10f, 180f, -12f, -2f, 52f, 26f)]
    [InlineData(10f, 90f, -2f, -12f, 42f, 36f)]
    [InlineData(10f, -90f, -2f, -12f, 42f, 36f)]
    [InlineData(0.5f, 0f, -3f, -2f, 43f, 26f)]
    public void ALinearShiftGrowsTheOutputBoundsAlongItsDirection(float distance, float angle, float left, float top, float right, float bottom)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));

        var bounds = Bounds(context, source, effect =>
        {
            effect.Distance = distance;
            effect.Angle = angle;
        });

        Assert.Equal(left, bounds.Left, 0.01);
        Assert.Equal(top, bounds.Top, 0.01);
        Assert.Equal(right, bounds.Right, 0.01);
        Assert.Equal(bottom, bounds.Bottom, 0.01);
    }

    [Fact]
    public void TheLinearBoundsIgnoreTheCenterOffset()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));

        var bounds = Bounds(context, source, effect =>
        {
            effect.Distance = 10f;
            effect.CenterX = 300f;
            effect.CenterY = -200f;
        });

        Assert.Equal((-12f, -2f, 52f, 26f), bounds);
    }

    [Fact]
    public void ARadialShiftZoomsTheOutputBoundsAboutTheImageCenter()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 30, Bgra.Opaque(10, 20, 30));

        var bounds = Bounds(context, source, effect =>
        {
            effect.Mode = (int)ShiftMode.Radial;
            effect.Distance = 12.5f;
        });

        Assert.Equal((-22f, -17f, 62f, 47f), bounds);
    }

    [Theory]
    [InlineData(0f, 0f, -7f, -12f, 47f, 42f)]
    [InlineData(10f, 0f, -6f, -21f, 66f, 51f)]
    public void ARotationTurnsTheOutputBoundsIntoACircleAboutTheCenter(float centerX, float centerY, float left, float top, float right, float bottom)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 30, Bgra.Opaque(10, 20, 30));

        var bounds = Bounds(context, source, effect =>
        {
            effect.Mode = (int)ShiftMode.Rotational;
            effect.Distance = MaxInputPixel;
            effect.CenterX = centerX;
            effect.CenterY = centerY;
        });

        Assert.Equal((left, top, right, bottom), bounds);
    }

    [Fact]
    public void ChangingTheCenterAfterTheInputWasSetMovesTheBounds()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 30, Bgra.Opaque(10, 20, 30));
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        effect.Mode = (int)ShiftMode.Rotational;
        effect.Distance = MaxInputPixel;
        using var output = effect.Output;
        var centered = context.DeviceContext.GetImageLocalBounds(output);

        effect.CenterX = 10f;
        var moved = context.DeviceContext.GetImageLocalBounds(output);

        Assert.Equal(-7f, centered.Left);
        Assert.Equal(-6f, moved.Left);
        Assert.Equal(66f, moved.Right);
    }

    [Theory]
    [InlineData(ShiftMode.Linear)]
    [InlineData(ShiftMode.Radial)]
    [InlineData(ShiftMode.Rotational)]
    public void TheSpectrumSettingsDoNotChangeTheOutputBounds(ShiftMode mode)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 30, Bgra.Opaque(10, 20, 30));

        var plain = Bounds(context, source, effect =>
        {
            effect.Mode = (int)mode;
            effect.Distance = 12f;
        });
        var spectral = Bounds(context, source, effect =>
        {
            effect.Mode = (int)mode;
            effect.Distance = 12f;
            effect.Dispersion = (int)DispersionLaw.Prism;
            effect.ReferenceWavelength = 450f;
            effect.Steps = 8;
            effect.Strength = 0.5f;
        });

        Assert.Equal(plain, spectral);
    }

    [Fact]
    public void TheOutputMarginIsCappedAtTheInputPixelLimit()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 8, 8, Bgra.Opaque(10, 20, 30));

        var bounds = Bounds(context, source, effect => effect.Distance = float.MaxValue);

        Assert.Equal(-(MaxInputPixel + 2f), bounds.Left);
        Assert.Equal(8f + MaxInputPixel + 2f, bounds.Right);
        Assert.Equal(-2f, bounds.Top);
        Assert.Equal(10f, bounds.Bottom);
    }

    [Theory]
    [InlineData(ShiftMode.Linear, 10f, 5f, 10f, 20f)]
    [InlineData(ShiftMode.Radial, 10f, 5f, 30f, 5f)]
    [InlineData(ShiftMode.Rotational, 10f, 5f, 10f, 5f)]
    public void AnEmptyInputStaysEmpty(ShiftMode mode, float left, float top, float right, float bottom)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));
        using var crop = new Crop(context.DeviceContext) { Rectangle = new Vector4(left, top, right, bottom) };
        crop.SetInput(0, source.Bitmap, true);
        using var cropped = crop.Output;
        using var effect = new SpectrumColorShiftCustomEffect(context);
        effect.SetInput(0, cropped, true);
        effect.Mode = (int)mode;
        effect.Distance = 3f;
        using var output = effect.Output;

        var bounds = context.DeviceContext.GetImageLocalBounds(output);

        Assert.True(bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top, $"{bounds}");
    }
}
