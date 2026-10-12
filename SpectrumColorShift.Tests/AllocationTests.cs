using System.Numerics;
using Vortice;
using static SpectrumColorShift.SpectrumColorShiftCustomEffect;

namespace SpectrumColorShift.Tests;

public sealed class AllocationTests
{
    [Theory]
    [InlineData(ShiftMode.Linear)]
    [InlineData(ShiftMode.Radial)]
    [InlineData(ShiftMode.Rotational)]
    public void MappingTheRectsAllocatesNothing(ShiftMode mode)
    {
        var impl = new EffectImpl { Mode = (int)mode, Distance = 24f, Angle = 30f };
        RawRect[] inputs = [new RawRect(0, 0, 640, 480)];
        RawRect[] opaque = [new RawRect(0, 0, 640, 480)];
        RawRect[] requested = new RawRect[1];
        var tile = new RawRect(100, 100, 200, 200);

        var allocated = Allocation.Of(() =>
        {
            impl.MapInputRectsToOutputRect(inputs, opaque, out _, out _);
            impl.MapOutputRectToInputRects(tile, requested);
            impl.MapInvalidRect(0, tile);
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ChangingTheSettingsAllocatesNothing()
    {
        var impl = new EffectImpl();
        var step = 0;

        var allocated = Allocation.Of(() =>
        {
            step++;
            impl.Mode = 1 << (step % 3);
            impl.Distance = step % 50;
            impl.Angle = step % 360;
            impl.CenterX = step % 100;
            impl.CenterY = step % 100;
            impl.Strength = step % 100 / 100f;
            impl.Dispersion = step & 1;
            impl.ReferenceWavelength = 400f + step % 300;
            impl.Steps = Spectrum.MinSteps + step % (Spectrum.MaxSteps - Spectrum.MinSteps + 1);
        });

        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(DispersionLaw.Grating)]
    [InlineData(DispersionLaw.Prism)]
    public void FillingTheTableAllocatesNothing(DispersionLaw law)
    {
        var table = new Vector4[Spectrum.MaxSteps];
        var step = 0;

        var allocated = Allocation.Of(() =>
        {
            step++;
            Spectrum.Fill(table, Spectrum.MinSteps + step % (Spectrum.MaxSteps - Spectrum.MinSteps + 1), 400f + step % 300, law);
        });

        Assert.Equal(0, allocated);
    }
}
