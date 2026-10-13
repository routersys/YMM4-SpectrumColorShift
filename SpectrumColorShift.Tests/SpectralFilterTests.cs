using System.Numerics;

namespace SpectrumColorShift.Tests;

public sealed class SpectralFilterTests
{
    const int SignalLength = 256;

    static Vector4[] Spectral(int steps, DispersionLaw law, float reference = 550f)
    {
        var table = new Vector4[Spectrum.SpectralTableLength];
        Spectrum.FillSpectral(table, steps, reference, law);

        return table;
    }

    static Vector2 Direction(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;

        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    static Vector4 Signal(int position)
    {
        var seed = (uint)(position * 2654435761u);
        float Next()
        {
            seed = seed * 1664525u + 1013904223u;

            return (seed >> 8) / (float)(1 << 24);
        }

        return new Vector4(Next(), Next(), Next(), Next());
    }

    static Vector3 Rgb(Vector4 value) => new(value.X, value.Y, value.Z);

    static (Vector3 Color, float Alpha) Stepwise(Vector4[] table, int steps, float distance, float along, int pixel)
    {
        var color = Vector3.Zero;
        var alpha = 0f;
        for (var step = 0; step < steps; step++)
        {
            var gain = table[step * Spectrum.EntriesPerStep];
            var basis = table[step * Spectrum.EntriesPerStep + 1];
            var offset = -(double)basis.W * distance * along;
            var floor = (int)Math.Floor(offset);
            var fraction = (float)(offset - floor);
            var shifted = Vector4.Lerp(Signal(pixel + floor), Signal(pixel + floor + 1), fraction);
            color += Rgb(gain) * Vector3.Dot(Rgb(basis), Rgb(shifted));
            alpha += gain.W * shifted.W;
        }

        return (color, alpha);
    }

    static (Vector3 Color, float Alpha) Filtered(Vector4[] taps, SpectralFilter.Layout layout, int pixel)
    {
        var color = Vector3.Zero;
        var alpha = 0f;
        for (var tap = 0; tap < layout.Taps; tap++)
        {
            var shifted = Signal(pixel + layout.Origin + tap);
            var red = taps[tap * SpectralFilter.EntriesPerTap];
            var green = taps[tap * SpectralFilter.EntriesPerTap + 1];
            var blue = taps[tap * SpectralFilter.EntriesPerTap + 2];
            color += new Vector3(Vector3.Dot(Rgb(red), Rgb(shifted)), Vector3.Dot(Rgb(green), Rgb(shifted)), Vector3.Dot(Rgb(blue), Rgb(shifted)));
            alpha += red.W * shifted.W;
        }

        return (color, alpha);
    }

    public static readonly TheoryData<float, float, int, DispersionLaw> Shifts = new()
    {
        { 0f, 8f, 32, DispersionLaw.Grating },
        { 180f, 8f, 32, DispersionLaw.Grating },
        { 90f, 6.5f, 24, DispersionLaw.Prism },
        { 270f, -5f, 24, DispersionLaw.Grating },
        { 0f, 2f, 16, DispersionLaw.Grating },
        { 0f, 0.4f, 32, DispersionLaw.Prism },
        { 180f, 18.75f, 64, DispersionLaw.Grating },
        { 90f, -12.3f, 48, DispersionLaw.Prism },
    };

    [Theory]
    [MemberData(nameof(Shifts))]
    public void TheTapsReproduceTheStepwiseBilinearSum(float angle, float distance, int steps, DispersionLaw law)
    {
        var table = Spectral(steps, law);
        var taps = new Vector4[SpectralFilter.TableLength];
        var direction = Direction(angle);

        var layout = SpectralFilter.Fill(taps, table, steps, distance, direction);

        Assert.True(layout.Taps > 0);
        var along = layout.Vertical ? direction.Y : direction.X;
        for (var pixel = 64; pixel < 128; pixel++)
        {
            var expected = Stepwise(table, steps, distance, along, pixel);
            var actual = Filtered(taps, layout, pixel);
            Assert.True(Vector3.Distance(expected.Color, actual.Color) <= 2e-5f && Math.Abs(expected.Alpha - actual.Alpha) <= 2e-5f, $"pixel {pixel}: {expected} against {actual}");
        }
    }

    [Theory]
    [MemberData(nameof(Shifts))]
    public void TheTapsSumToTheIdentityAndAUnitAlpha(float angle, float distance, int steps, DispersionLaw law)
    {
        var table = Spectral(steps, law);
        var taps = new Vector4[SpectralFilter.TableLength];

        var layout = SpectralFilter.Fill(taps, table, steps, distance, Direction(angle));

        var rows = new Vector3[3];
        var alpha = 0f;
        for (var tap = 0; tap < layout.Taps; tap++)
        {
            for (var row = 0; row < 3; row++)
                rows[row] += Rgb(taps[tap * SpectralFilter.EntriesPerTap + row]);

            alpha += taps[tap * SpectralFilter.EntriesPerTap].W;
        }

        Assert.True(Vector3.Distance(rows[0], Vector3.UnitX) <= 1e-5f, $"{rows[0]}");
        Assert.True(Vector3.Distance(rows[1], Vector3.UnitY) <= 1e-5f, $"{rows[1]}");
        Assert.True(Vector3.Distance(rows[2], Vector3.UnitZ) <= 1e-5f, $"{rows[2]}");
        Assert.Equal(1f, alpha, 1e-5f);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(180f, false)]
    [InlineData(90f, true)]
    [InlineData(270f, true)]
    public void TheAxisOfTheDirectionDecidesWhetherTheTapsRunVertically(float angle, bool vertical)
    {
        var taps = new Vector4[SpectralFilter.TableLength];

        var layout = SpectralFilter.Fill(taps, Spectral(32, DispersionLaw.Grating), 32, 8f, Direction(angle));

        Assert.True(layout.Taps > 0);
        Assert.Equal(vertical, layout.Vertical);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(30f)]
    [InlineData(45f)]
    [InlineData(89f)]
    [InlineData(135f)]
    [InlineData(200f)]
    public void ADirectionOffTheAxesUsesNoTaps(float angle)
    {
        var taps = new Vector4[SpectralFilter.TableLength];

        var layout = SpectralFilter.Fill(taps, Spectral(32, DispersionLaw.Grating), 32, 8f, Direction(angle));

        Assert.Equal(default, layout);
    }

    [Theory]
    [InlineData(0f, 32, 40f)]
    [InlineData(0f, 64, 60f)]
    [InlineData(0f, 8, 6f)]
    [InlineData(90f, 32, 24f)]
    public void ADistanceTooLongForTheTapsUsesNoTaps(float angle, int steps, float distance)
    {
        var taps = new Vector4[SpectralFilter.TableLength];

        var layout = SpectralFilter.Fill(taps, Spectral(steps, DispersionLaw.Grating), steps, distance, Direction(angle));

        Assert.Equal(default, layout);
    }

    [Fact]
    public void NoDistanceUsesNoTaps()
    {
        var taps = new Vector4[SpectralFilter.TableLength];

        var layout = SpectralFilter.Fill(taps, Spectral(32, DispersionLaw.Grating), 32, 0f, Direction(0f));

        Assert.Equal(default, layout);
    }

    [Fact]
    public void TheTapsAreWholeQuantaAndStayWithinTheShareOfTheSteps()
    {
        var taps = new Vector4[SpectralFilter.TableLength];
        var used = 0;
        foreach (var steps in new[] { 8, 12, 16, 24, 32, 40, 48, 64 })
        {
            var table = Spectral(steps, DispersionLaw.Grating);
            for (var distance = 0.25f; distance <= 30f; distance += 0.25f)
            {
                var layout = SpectralFilter.Fill(taps, table, steps, distance, Direction(0f));
                if (layout.Taps == 0)
                    continue;

                used++;
                Assert.Equal(0, layout.Taps % SpectralFilter.TapQuantum);
                Assert.InRange(layout.Taps, SpectralFilter.TapQuantum, SpectralFilter.MaxTaps);
                Assert.True(layout.Taps <= Spectrum.Padded(steps) * SpectralFilter.MaxTapShare, $"{layout.Taps} taps for {steps} steps at {distance}");
            }
        }

        Assert.True(used > 100, $"{used}");
    }

    [Fact]
    public void TheEntriesBeyondTheTapsAreCleared()
    {
        var taps = new Vector4[SpectralFilter.TableLength];
        Array.Fill(taps, new Vector4(7f));

        var layout = SpectralFilter.Fill(taps, Spectral(32, DispersionLaw.Grating), 32, 8f, Direction(0f));

        Assert.All(taps[(layout.Taps * SpectralFilter.EntriesPerTap)..], entry => Assert.Equal(Vector4.Zero, entry));
    }
}
