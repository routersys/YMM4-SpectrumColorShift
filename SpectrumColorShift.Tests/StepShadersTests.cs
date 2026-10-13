using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SpectrumColorShift.Tests;

public sealed partial class StepShadersTests
{
    static string Project { get; } = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "SpectrumColorShift.Shader", "SpectrumColorShift.Shader.vcxproj"));

    static string Here([CallerFilePath] string path = "") => path;

    [GeneratedRegex(@"<StepCount Include=""(?<counts>[^""]+)""")]
    private static partial Regex CountsPattern();

    [Fact]
    public void TheCountsCoverTheWholeRangeInWholeQuanta()
    {
        Assert.Equal(0, Spectrum.MinSteps % Spectrum.StepQuantum);
        Assert.Equal(0, Spectrum.MaxSteps % Spectrum.StepQuantum);
        Assert.Equal(Enumerable.Range(0, (Spectrum.MaxSteps - Spectrum.MinSteps) / Spectrum.StepQuantum + 1).Select(index => Spectrum.MinSteps + index * Spectrum.StepQuantum), StepShaders.Counts);
    }

    [Fact]
    public void TheShaderProjectCompilesExactlyTheseCounts()
    {
        var compiled = CountsPattern().Match(Project).Groups["counts"].Value.Split(';').Select(count => int.Parse(count, CultureInfo.InvariantCulture));

        Assert.Equal(StepShaders.Counts, compiled);
    }

    [Fact]
    public void EveryCountHasItsOwnShaderInEachFamily()
    {
        var ids = StepShaders.Counts.SelectMany(count => new[] { StepShaders.Select(false, count), StepShaders.Select(true, count) }).ToList();

        Assert.Equal(2 * StepShaders.Counts.Count(), ids.Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StepsShareTheShaderOfTheirPaddedCount(bool physical)
    {
        foreach (var steps in Enumerable.Range(Spectrum.MinSteps, Spectrum.MaxSteps - Spectrum.MinSteps + 1))
            Assert.Equal(StepShaders.Select(physical, Spectrum.Padded(steps)), StepShaders.Select(physical, steps));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryCountIsShippedAsAResource(bool physical)
    {
        foreach (var count in StepShaders.Counts)
        {
            var bytes = YukkuriMovieMaker.Commons.PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(StepShaders.ResourceName(physical, count)));
            Assert.True(bytes.Length > 0, $"{StepShaders.ResourceName(physical, count)}");
        }
    }
}
