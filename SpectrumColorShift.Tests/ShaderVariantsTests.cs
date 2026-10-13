using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift.Tests;

public sealed partial class ShaderVariantsTests
{
    static string Project { get; } = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "SpectrumColorShift.Shader", "SpectrumColorShift.Shader.vcxproj"));

    static string Here([CallerFilePath] string path = "") => path;

    static IEnumerable<(ShaderFamily Family, bool Physical)> SteppedFamilies => [(ShaderVariants.Legacy, false), (ShaderVariants.Physical, true)];

    [GeneratedRegex(@"<StepCount Include=""(?<counts>[^""]+)""")]
    private static partial Regex StepCountsPattern();

    [GeneratedRegex(@"<TapCount Include=""(?<counts>[^""]+)""")]
    private static partial Regex TapCountsPattern();

    [GeneratedRegex(@"ShaderMode=(?<value>\d+)</PreprocessorDefinitions>\s*<ObjectFileOutput>\$\(OutDir\)(?<prefix>SpectrumColorShiftPhysicalFilter|SpectrumColorShiftPhysical|SpectrumColorShift)(?<mode>[A-Za-z]+)%\((?<list>StepCount|TapCount)\.Identity\)\.cso")]
    private static partial Regex VariantPattern();

    static IEnumerable<int> Listed(Regex pattern) => pattern.Match(Project).Groups["counts"].Value.Split(';').Select(count => int.Parse(count, CultureInfo.InvariantCulture));

    [Fact]
    public void TheStepCountsCoverTheWholeRangeInWholeQuanta()
    {
        Assert.Equal(0, Spectrum.MinSteps % Spectrum.StepQuantum);
        Assert.Equal(0, Spectrum.MaxSteps % Spectrum.StepQuantum);
        Assert.Equal(Enumerable.Range(0, (Spectrum.MaxSteps - Spectrum.MinSteps) / Spectrum.StepQuantum + 1).Select(index => Spectrum.MinSteps + index * Spectrum.StepQuantum), ShaderVariants.Legacy.Counts);
        Assert.Equal(ShaderVariants.Legacy.Counts, ShaderVariants.Physical.Counts);
    }

    [Fact]
    public void TheTapCountsCoverTheWholeRangeInWholeQuanta()
    {
        Assert.Equal(0, SpectralFilter.MaxTaps % SpectralFilter.TapQuantum);
        Assert.Equal(Enumerable.Range(1, SpectralFilter.MaxTaps / SpectralFilter.TapQuantum).Select(index => index * SpectralFilter.TapQuantum), ShaderVariants.Filter.Counts);
    }

    [Fact]
    public void TheShaderProjectCompilesExactlyTheseCounts()
    {
        Assert.Equal(ShaderVariants.Legacy.Counts, Listed(StepCountsPattern()));
        Assert.Equal(ShaderVariants.Filter.Counts, Listed(TapCountsPattern()));
    }

    [Fact]
    public void TheShaderProjectCompilesEveryModeOfEveryFamilyWithItsEnumValue()
    {
        var compiled = VariantPattern().Matches(Project).Select(match => (match.Groups["prefix"].Value, match.Groups["mode"].Value, int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture), match.Groups["list"].Value));
        var stepped = from mode in ShaderVariants.Legacy.Modes from prefix in new[] { "SpectrumColorShift", "SpectrumColorShiftPhysical" } select (prefix, mode.ToString(), (int)mode, "StepCount");
        var expected = stepped.Append(("SpectrumColorShiftPhysicalFilter", ShiftMode.Linear.ToString(), (int)ShiftMode.Linear, "TapCount"));

        Assert.Equal(expected.Order(), compiled.Order());
    }

    [Fact]
    public void EveryModeAndCountHasItsOwnShaderInEveryFamily()
    {
        var families = new[] { ShaderVariants.Legacy, ShaderVariants.Physical, ShaderVariants.Filter };
        var ids = (from family in families from mode in family.Modes from count in family.Counts select family.Id(mode, count)).ToList();

        Assert.Equal(families.Sum(family => family.Modes.Count * family.Counts.Count()), ids.Distinct().Count());
    }

    [Fact]
    public void StepsShareTheShaderOfTheirPaddedCount()
    {
        foreach (var (family, _) in SteppedFamilies)
        {
            foreach (var mode in family.Modes)
            {
                foreach (var steps in Enumerable.Range(Spectrum.MinSteps, Spectrum.MaxSteps - Spectrum.MinSteps + 1))
                    Assert.Equal(family.Id(mode, Spectrum.Padded(steps)), family.Id(mode, steps));
            }
        }
    }

    [Fact]
    public void AModeOutsideAFamilyHasNoShader()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShaderVariants.Filter.Id(ShiftMode.Radial, 8));
    }

    [Fact]
    public void EveryVariantIsShippedAsAResource()
    {
        foreach (var family in new[] { ShaderVariants.Legacy, ShaderVariants.Physical, ShaderVariants.Filter })
        {
            foreach (var mode in family.Modes)
            {
                foreach (var count in family.Counts)
                {
                    var name = family.ResourceName(mode, count);
                    Assert.True(PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(name)).Length > 0, name);
                }
            }
        }
    }
}
