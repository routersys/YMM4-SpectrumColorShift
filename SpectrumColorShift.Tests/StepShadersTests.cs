using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift.Tests;

public sealed partial class StepShadersTests
{
    static string Project { get; } = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "SpectrumColorShift.Shader", "SpectrumColorShift.Shader.vcxproj"));

    static string Here([CallerFilePath] string path = "") => path;

    [GeneratedRegex(@"<StepCount Include=""(?<counts>[^""]+)""")]
    private static partial Regex CountsPattern();

    [GeneratedRegex(@"ShaderMode=(?<value>\d+)</PreprocessorDefinitions>\s*<ObjectFileOutput>\$\(OutDir\)(?<prefix>SpectrumColorShift(?:Physical)?)(?<mode>[A-Za-z]+)%\(StepCount\.Identity\)\.cso")]
    private static partial Regex VariantPattern();

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
    public void TheShaderProjectCompilesEveryModeOfBothFamiliesWithItsEnumValue()
    {
        var compiled = VariantPattern().Matches(Project).Select(match => (match.Groups["prefix"].Value, match.Groups["mode"].Value, int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture)));
        var expected = from prefix in new[] { "SpectrumColorShift", "SpectrumColorShiftPhysical" } from mode in StepShaders.Modes select (prefix, mode.ToString(), (int)mode);

        Assert.Equal(expected.Order(), compiled.Order());
    }

    [Fact]
    public void EveryModeAndCountHasItsOwnShaderInEachFamily()
    {
        var ids = (from physical in new[] { false, true } from mode in StepShaders.Modes from count in StepShaders.Counts select StepShaders.Select(physical, mode, count)).ToList();

        Assert.Equal(2 * StepShaders.Modes.Count() * StepShaders.Counts.Count(), ids.Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StepsShareTheShaderOfTheirPaddedCount(bool physical)
    {
        foreach (var mode in StepShaders.Modes)
        {
            foreach (var steps in Enumerable.Range(Spectrum.MinSteps, Spectrum.MaxSteps - Spectrum.MinSteps + 1))
                Assert.Equal(StepShaders.Select(physical, mode, Spectrum.Padded(steps)), StepShaders.Select(physical, mode, steps));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryVariantIsShippedAsAResource(bool physical)
    {
        foreach (var mode in StepShaders.Modes)
        {
            foreach (var count in StepShaders.Counts)
            {
                var name = StepShaders.ResourceName(physical, mode, count);
                Assert.True(PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(name)).Length > 0, name);
            }
        }
    }
}
