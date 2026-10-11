using System.Numerics;
using Vortice;

namespace SpectrumColorShift.Tests;

public sealed class ShiftGeometryTests
{
    static readonly Vector2 Horizontal = Vector2.UnitX;

    static Vector2 Unit(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    static IEnumerable<float> Coefficients() => Enumerable.Range(0, 21).Select(index => index / 10f - 1f);

    static IEnumerable<Vector2> Points(RawRect rect, float step)
    {
        for (var y = (float)rect.Top; y <= rect.Bottom; y += step)
        {
            for (var x = (float)rect.Left; x <= rect.Right; x += step)
                yield return new Vector2(x, y);
        }
    }

    static bool Contains(RawRect rect, Vector2 point, float slack = 0f)
        => point.X >= rect.Left - slack && point.X <= rect.Right + slack && point.Y >= rect.Top - slack && point.Y <= rect.Bottom + slack;

    static RawRect Grow(RawRect rect, int margin) => new(rect.Left - margin, rect.Top - margin, rect.Right + margin, rect.Bottom + margin);

    [Fact]
    public void TheCenterIsTheMiddleOfTheRectangleMovedByTheOffset()
    {
        var center = ShiftGeometry.Center(new RawRect(-10, 4, 30, 28), new Vector2(5f, -3f));

        Assert.Equal(new Vector2(15f, 13f), center);
    }

    [Theory]
    [InlineData(0, 0, 40, 30, 20f, 15f, 25f)]
    [InlineData(0, 0, 40, 30, 0f, 0f, 50f)]
    [InlineData(0, 0, 40, 30, 40f, 30f, 50f)]
    [InlineData(-30, -40, 30, 40, 0f, 0f, 50f)]
    [InlineData(5, 5, 5, 5, 5f, 5f, 1f)]
    [InlineData(0, 0, 0, 0, 0.25f, 0.25f, 1f)]
    public void TheRadiusIsTheDistanceToTheFarthestCornerAndAtLeastOne(int left, int top, int right, int bottom, float x, float y, float expected)
    {
        Assert.Equal(expected, ShiftGeometry.Radius(new RawRect(left, top, right, bottom), new Vector2(x, y)), 1e-4);
    }

    [Theory]
    [InlineData(ShiftMode.Linear)]
    [InlineData(ShiftMode.Radial)]
    [InlineData(ShiftMode.Rotational)]
    public void WithoutADistanceTheBoundsStayTheSame(ShiftMode mode)
    {
        var rect = new RawRect(3, -4, 43, 20);

        Assert.Equal(rect, ShiftGeometry.Output(rect, mode, 0f, Horizontal, new Vector2(23f, 8f), 24f));
        Assert.Equal(rect, ShiftGeometry.Input(rect, mode, 0f, Horizontal, new Vector2(23f, 8f), 24f));
    }

    [Theory]
    [InlineData(10f, 1f, 0f, -12, -2, 52, 26)]
    [InlineData(-10f, 1f, 0f, -12, -2, 52, 26)]
    [InlineData(10f, 0f, 1f, -2, -12, 42, 36)]
    [InlineData(10f, 0.5f, 0.25f, -7, -5, 47, 29)]
    [InlineData(0.25f, 1f, 0f, -3, -2, 43, 26)]
    [InlineData(10.0005f, 1f, 0f, -12, -2, 52, 26)]
    [InlineData(10.002f, 1f, 0f, -13, -2, 53, 26)]
    public void ALinearShiftGrowsTheBoundsByTheRoundedUpComponentsPlusTwoPixels(float distance, float x, float y, int left, int top, int right, int bottom)
    {
        var rect = new RawRect(0, 0, 40, 24);

        var output = ShiftGeometry.Output(rect, ShiftMode.Linear, distance, new Vector2(x, y), Vector2.Zero, 1f);
        var input = ShiftGeometry.Input(rect, ShiftMode.Linear, distance, new Vector2(x, y), Vector2.Zero, 1f);

        Assert.Equal(new RawRect(left, top, right, bottom), output);
        Assert.Equal(output, input);
    }

    [Theory]
    [InlineData(90f, -2, -12, 42, 36)]
    [InlineData(-90f, -2, -12, 42, 36)]
    [InlineData(180f, -12, -2, 52, 26)]
    [InlineData(270f, -2, -12, 42, 36)]
    [InlineData(0f, -12, -2, 52, 26)]
    public void AnAxisAlignedAngleAddsNoPixelFromRoundingNoise(float degrees, int left, int top, int right, int bottom)
    {
        var output = ShiftGeometry.Output(new RawRect(0, 0, 40, 24), ShiftMode.Linear, 10f, Unit(degrees), Vector2.Zero, 1f);

        Assert.Equal(new RawRect(left, top, right, bottom), output);
    }

    [Theory]
    [InlineData(25f, -69, -69, 69, 69)]
    [InlineData(-25f, -69, -69, 69, 69)]
    [InlineData(50f, -102, -102, 102, 102)]
    [InlineData(1000f, -102, -102, 102, 102)]
    public void ARadialShiftZoomsTheBoundsAboutTheCenterAndStopsAtADoubling(float distance, int left, int top, int right, int bottom)
    {
        var rect = new RawRect(-50, -50, 50, 50);

        var output = ShiftGeometry.Output(rect, ShiftMode.Radial, distance, Horizontal, Vector2.Zero, 100f);

        Assert.Equal(new RawRect(left, top, right, bottom), output);
    }

    [Fact]
    public void ARadialShiftAboutAMovedCenterZoomsEachSideByItsOwnDistance()
    {
        var output = ShiftGeometry.Output(new RawRect(0, 0, 40, 20), ShiftMode.Radial, 50f, Horizontal, new Vector2(10f, 0f), 100f);

        Assert.Equal(new RawRect(-12, -2, 72, 42), output);
    }

    [Fact]
    public void ARadialShiftOfAMatchingRadiusZoomsTheBoundsOfARealImage()
    {
        var rect = new RawRect(0, 0, 40, 30);
        var center = ShiftGeometry.Center(rect, Vector2.Zero);
        var radius = ShiftGeometry.Radius(rect, center);

        var output = ShiftGeometry.Output(rect, ShiftMode.Radial, radius * 0.5f, Horizontal, center, radius);

        Assert.Equal(new RawRect(-22, -17, 62, 47), output);
    }

    [Theory]
    [InlineData(10f, -57, -57, 57, 57)]
    [InlineData(314.159265f, -73, -73, 73, 73)]
    [InlineData(5000f, -73, -73, 73, 73)]
    public void ARotationSweepsTheCornersAlongTheirCircles(float distance, int left, int top, int right, int bottom)
    {
        var rect = new RawRect(-50, -50, 50, 50);

        var output = ShiftGeometry.Output(rect, ShiftMode.Rotational, distance, Horizontal, Vector2.Zero, 100f);

        Assert.Equal(new RawRect(left, top, right, bottom), output);
    }

    [Theory]
    [InlineData(0f, 0f, -7, -12, 47, 42)]
    [InlineData(10f, 0f, -6, -21, 66, 51)]
    public void AHalfTurnFillsTheCircleThroughTheFarthestCorner(float centerX, float centerY, int left, int top, int right, int bottom)
    {
        var rect = new RawRect(0, 0, 40, 30);
        var center = ShiftGeometry.Center(rect, new Vector2(centerX, centerY));
        var radius = ShiftGeometry.Radius(rect, center);

        var output = ShiftGeometry.Output(rect, ShiftMode.Rotational, 100000f, Horizontal, center, radius);

        Assert.Equal(new RawRect(left, top, right, bottom), output);
    }

    [Fact]
    public void ARotationAboutACornerSweepsAWholeCircleAroundIt()
    {
        var output = ShiftGeometry.Output(new RawRect(0, 0, 100, 100), ShiftMode.Rotational, 100000f, Horizontal, Vector2.Zero, 100f);

        Assert.Equal(new RawRect(-144, -144, 144, 144), output);
    }

    [Theory]
    [InlineData(25f, -65, -65, 65, 65)]
    [InlineData(-25f, -65, -65, 65, 65)]
    [InlineData(1000f, -77, -77, 77, 77)]
    public void TheRadialInputCoversTheTravelOfTheFarthestPoint(float distance, int left, int top, int right, int bottom)
    {
        var tile = new RawRect(-50, -50, 50, 50);

        var input = ShiftGeometry.Input(tile, ShiftMode.Radial, distance, Horizontal, Vector2.Zero, 100f);

        Assert.Equal(new RawRect(left, top, right, bottom), input);
    }

    [Fact]
    public void TheRadialInputGrowsEachAxisByItsOwnTravel()
    {
        var input = ShiftGeometry.Input(new RawRect(0, 0, 40, 20), ShiftMode.Radial, 50f, Horizontal, new Vector2(10f, 0f), 100f);

        Assert.Equal(new RawRect(-17, -12, 57, 32), input);
    }

    [Theory]
    [InlineData(10f, -60, -60, 60, 60)]
    [InlineData(100000f, -194, -194, 194, 194)]
    public void TheRotationalInputCoversTheChordOfTheFarthestPoint(float distance, int left, int top, int right, int bottom)
    {
        var tile = new RawRect(-50, -50, 50, 50);

        var input = ShiftGeometry.Input(tile, ShiftMode.Rotational, distance, Horizontal, Vector2.Zero, 100f);

        Assert.Equal(new RawRect(left, top, right, bottom), input);
    }

    [Fact]
    public void AnUnknownModeIsTreatedAsLinear()
    {
        var rect = new RawRect(0, 0, 40, 24);

        Assert.Equal(
            ShiftGeometry.Output(rect, ShiftMode.Linear, 10f, Horizontal, Vector2.Zero, 1f),
            ShiftGeometry.Output(rect, (ShiftMode)0, 10f, Horizontal, Vector2.Zero, 1f));
        Assert.Equal(
            ShiftGeometry.Input(rect, ShiftMode.Linear, 10f, Horizontal, Vector2.Zero, 1f),
            ShiftGeometry.Input(rect, (ShiftMode)3, 10f, Horizontal, Vector2.Zero, 1f));
    }

    [Fact]
    public void TheBoundsSaturateAtTheIntegerLimits()
    {
        var rect = new RawRect(int.MinValue + 1, int.MinValue, int.MaxValue - 1, int.MaxValue);

        var output = ShiftGeometry.Output(rect, ShiftMode.Linear, 10f, Horizontal, Vector2.Zero, 1f);

        Assert.Equal(new RawRect(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue), output);
    }

    public static readonly TheoryData<ShiftMode, float, float, float, float> Shifts = new()
    {
        { ShiftMode.Linear, 12f, 30f, 0f, 0f },
        { ShiftMode.Linear, -40f, 135f, 0f, 0f },
        { ShiftMode.Linear, 5f, 17f, 0f, 0f },
        { ShiftMode.Radial, 12f, 0f, 0f, 0f },
        { ShiftMode.Radial, 40f, 0f, 30f, -20f },
        { ShiftMode.Radial, -25f, 0f, -15f, 8f },
        { ShiftMode.Radial, 900f, 0f, 10f, 0f },
        { ShiftMode.Rotational, 12f, 0f, 0f, 0f },
        { ShiftMode.Rotational, 40f, 0f, 30f, -20f },
        { ShiftMode.Rotational, -25f, 0f, -15f, 8f },
        { ShiftMode.Rotational, 900f, 0f, 10f, 0f },
    };

    [Theory]
    [MemberData(nameof(Shifts))]
    public void EveryPointThatCanDrawFromTheImageLiesInsideTheOutputBounds(ShiftMode mode, float distance, float angle, float offsetX, float offsetY)
    {
        var rect = new RawRect(0, 0, 60, 40);
        var direction = Unit(angle);
        var center = ShiftGeometry.Center(rect, new Vector2(offsetX, offsetY));
        var radius = ShiftGeometry.Radius(rect, center);
        var output = ShiftGeometry.Output(rect, mode, distance, direction, center, radius);

        foreach (var point in Points(Grow(rect, 160), 2f))
        {
            foreach (var coefficient in Coefficients())
            {
                var source = ShiftReference.SamplePosition(mode, point, coefficient, distance, direction, center, radius);
                if (Contains(rect, source, 1f))
                    Assert.True(Contains(output, point), $"{point} samples {source} at {coefficient}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Shifts))]
    public void TheOutputBoundsAreNoWiderThanTheShiftNeeds(ShiftMode mode, float distance, float angle, float offsetX, float offsetY)
    {
        var rect = new RawRect(0, 0, 60, 40);
        var direction = Unit(angle);
        var center = ShiftGeometry.Center(rect, new Vector2(offsetX, offsetY));
        var radius = ShiftGeometry.Radius(rect, center);
        var output = ShiftGeometry.Output(rect, mode, distance, direction, center, radius);
        var reached = Points(Grow(rect, 160), 1f)
            .Where(point => Coefficients().Any(coefficient => Contains(rect, ShiftReference.SamplePosition(mode, point, coefficient, distance, direction, center, radius))))
            .ToArray();

        Assert.InRange(reached.Min(point => point.X) - output.Left, -1f, ShiftGeometry.Guard + 3f);
        Assert.InRange(reached.Min(point => point.Y) - output.Top, -1f, ShiftGeometry.Guard + 3f);
        Assert.InRange(output.Right - reached.Max(point => point.X), -1f, ShiftGeometry.Guard + 3f);
        Assert.InRange(output.Bottom - reached.Max(point => point.Y), -1f, ShiftGeometry.Guard + 3f);
    }

    [Theory]
    [MemberData(nameof(Shifts))]
    public void TheInputBoundsCoverEverySampleOfTheRequestedTile(ShiftMode mode, float distance, float angle, float offsetX, float offsetY)
    {
        var rect = new RawRect(0, 0, 60, 40);
        var tile = new RawRect(-30, -20, 70, 55);
        var direction = Unit(angle);
        var center = ShiftGeometry.Center(rect, new Vector2(offsetX, offsetY));
        var radius = ShiftGeometry.Radius(rect, center);
        var input = ShiftGeometry.Input(tile, mode, distance, direction, center, radius);

        foreach (var point in Points(tile, 1.5f))
        {
            foreach (var coefficient in Coefficients())
            {
                var source = ShiftReference.SamplePosition(mode, point, coefficient, distance, direction, center, radius);
                Assert.True(Contains(input, source), $"{point} samples {source} at {coefficient}");
            }
        }
    }
}
