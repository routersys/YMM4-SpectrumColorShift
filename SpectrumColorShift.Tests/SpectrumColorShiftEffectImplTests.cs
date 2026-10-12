using Vortice;
using static SpectrumColorShift.SpectrumColorShiftCustomEffect;

namespace SpectrumColorShift.Tests;

public sealed class SpectrumColorShiftEffectImplTests
{
    static RawRect Map(EffectImpl impl, RawRect input)
    {
        impl.MapInputRectsToOutputRect([input], [input], out var output, out _);

        return output;
    }

    static RawRect Request(EffectImpl impl, RawRect tile)
    {
        var inputs = new RawRect[1];
        impl.MapOutputRectToInputRects(tile, inputs);

        return inputs[0];
    }

    [Theory]
    [InlineData(10, 5, 10, 20)]
    [InlineData(10, 5, 30, 5)]
    [InlineData(10, 5, 10, 5)]
    [InlineData(30, 5, 10, 20)]
    [InlineData(10, 20, 30, 5)]
    public void AnEmptyInputIsPassedThroughAsItIs(int left, int top, int right, int bottom)
    {
        var impl = new EffectImpl { Distance = 5f };

        var output = Map(impl, new RawRect(left, top, right, bottom));

        Assert.Equal(new RawRect(left, top, right, bottom), output);
    }

    [Theory]
    [InlineData(10, 5, 10, 20)]
    [InlineData(10, 5, 30, 5)]
    public void AnEmptyInputIsRequestedBackWhateverTheTile(int left, int top, int right, int bottom)
    {
        var impl = new EffectImpl { Distance = 5f };
        Map(impl, new RawRect(left, top, right, bottom));

        var requested = Request(impl, new RawRect(0, 0, 50, 50));

        Assert.Equal(new RawRect(left, top, right, bottom), requested);
    }

    [Fact]
    public void BeforeAnyInputNothingIsRequested()
    {
        var impl = new EffectImpl { Distance = 5f };

        var requested = Request(impl, new RawRect(0, 0, 50, 50));

        Assert.Equal(default, requested);
    }

    [Fact]
    public void ATileIsRequestedWithTheMarginOfTheShift()
    {
        var impl = new EffectImpl { Distance = 5f };
        Map(impl, new RawRect(0, 0, 40, 24));

        var requested = Request(impl, new RawRect(0, 0, 50, 50));

        Assert.Equal(new RawRect(-7, -2, 57, 52), requested);
    }

    [Fact]
    public void TheLegacyShaderRequestsOnlyTheSourceInput()
    {
        var impl = new EffectImpl { Distance = 5f };
        Map(impl, new RawRect(0, 0, 40, 24));
        var inputs = new RawRect[2];

        impl.MapOutputRectToInputRects(new RawRect(0, 0, 50, 50), inputs);

        Assert.Equal([new RawRect(-7, -2, 57, 52), default], inputs);
    }

    [Fact]
    public void ThePhysicalShaderRequestsOnlyTheLinearizedInput()
    {
        var impl = new EffectImpl { Distance = 5f, Physical = 1 };
        Map(impl, new RawRect(0, 0, 40, 24));
        var inputs = new RawRect[2];

        impl.MapOutputRectToInputRects(new RawRect(0, 0, 50, 50), inputs);

        Assert.Equal([default, new RawRect(-7, -2, 57, 52)], inputs);
    }

    [Fact]
    public void ANoInputRequestIsIgnored()
    {
        var impl = new EffectImpl { Distance = 5f };
        Map(impl, new RawRect(0, 0, 40, 24));

        impl.MapOutputRectToInputRects(new RawRect(0, 0, 50, 50), []);
    }

    [Fact]
    public void AnInvalidInputInvalidatesTheShiftedOutput()
    {
        var impl = new EffectImpl { Distance = 10f };
        Map(impl, new RawRect(0, 0, 40, 24));

        var invalid = impl.MapInvalidRect(0, new RawRect(0, 0, 40, 24));

        Assert.Equal(new RawRect(-12, -2, 52, 26), invalid);
    }

    [Fact]
    public void EveryPropertyCanBeSetBeforeTheEffectIsDrawn()
    {
        var impl = new EffectImpl
        {
            Distance = 12f,
            Mode = (int)ShiftMode.Rotational,
            Angle = 45f,
            CenterX = 7f,
            CenterY = -3f,
            Strength = 0.5f,
            Dispersion = (int)DispersionLaw.Prism,
            ReferenceWavelength = 480f,
            Steps = 16,
            Physical = 1,
        };

        Assert.Equal(12f, impl.Distance);
        Assert.Equal((int)ShiftMode.Rotational, impl.Mode);
        Assert.Equal(45f, impl.Angle, 0.01);
        Assert.Equal(7f, impl.CenterX);
        Assert.Equal(-3f, impl.CenterY);
        Assert.Equal(0.5f, impl.Strength);
        Assert.Equal((int)DispersionLaw.Prism, impl.Dispersion);
        Assert.Equal(480f, impl.ReferenceWavelength);
        Assert.Equal(16, impl.Steps);
        Assert.Equal(1, impl.Physical);
    }
}
