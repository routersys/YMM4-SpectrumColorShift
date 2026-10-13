using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static SpectrumColorShift.SpectrumColorShiftCustomEffect;

namespace SpectrumColorShift.Tests;

public sealed partial class ShaderContractTests
{
    static readonly Dictionary<string, string> Fields = new()
    {
        ["inputBounds"] = "Header.InputBounds",
        ["direction"] = "Header.Direction",
        ["center"] = "Header.Center",
        ["shiftDistance"] = "Header.Distance",
        ["radius"] = "Header.Radius",
        ["strength"] = "Header.Strength",
        ["steps"] = "Header.Steps",
        ["spreadScale"] = "SpreadScale",
        ["samples"] = "Samples",
    };

    static string Source { get; } = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "SpectrumColorShift.Shader", "SpectrumColorShift.hlsl"));

    static string Here([CallerFilePath] string path = "") => path;

    static Type ConstantBuffer => typeof(EffectImpl).GetNestedType("ConstantBuffer", BindingFlags.NonPublic)!;

    [GeneratedRegex(@"static const (?:int|float) (?<name>\w+) = (?<value>[0-9.]+);")]
    private static partial Regex ConstantPattern();

    static float Constant(string name)
        => float.Parse(ConstantPattern().Matches(Source).Single(match => match.Groups["name"].Value == name).Groups["value"].Value, CultureInfo.InvariantCulture);

    [Fact]
    public void TheShaderAndTheEffectAgreeOnTheConstants()
    {
        Assert.Equal(Spectrum.MaxSteps, Constant("MaxSteps"));
        Assert.Equal((int)ShiftMode.Radial, Constant("ModeRadial"));
        Assert.Equal((int)ShiftMode.Rotational, Constant("ModeRotational"));
        Assert.Equal(ShiftGeometry.MaxRadialShift, Constant("MaxRadialShift"));
    }

    [Fact]
    public void TheConstantBufferMatchesTheShaderLayout()
    {
        var members = ConstantBufferContract.Declared(Source);

        Assert.Equal(Fields.Keys.Order(), members.Keys.Order());
        Assert.All(members, member => Assert.Equal(member.Value, ConstantBufferContract.OffsetOf(ConstantBuffer, Fields[member.Key])));
    }

    [Fact]
    public void TheConstantBufferFillsWholeRegistersAndHoldsTheTable()
    {
        Assert.Equal(16 * (4 + Spectrum.MaxSteps), ConstantBufferContract.SizeOf(ConstantBuffer));
    }
}
