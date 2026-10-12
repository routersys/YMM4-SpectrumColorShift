using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static SpectrumColorShift.SpectrumColorShiftCustomEffect;

namespace SpectrumColorShift.Tests;

public sealed partial class SpectralShaderContractTests
{
    static readonly Dictionary<string, string> Fields = new()
    {
        ["inputBounds"] = "InputBounds",
        ["direction"] = "Direction",
        ["center"] = "Center",
        ["shiftDistance"] = "Distance",
        ["radius"] = "Radius",
        ["strength"] = "Strength",
        ["mode"] = "Mode",
        ["steps"] = "Steps",
        ["spreadScale"] = "SpreadScale",
        ["samples"] = "Samples",
    };

    static string Source { get; } = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "SpectrumColorShift.Shader", "SpectrumColorShiftPhysical.hlsl"));

    static string Here([CallerFilePath] string path = "") => path;

    static Type ConstantBuffer => typeof(EffectImpl).GetNestedType("PhysicalConstantBuffer", BindingFlags.NonPublic)!;

    [GeneratedRegex(@"static const (?:int|float) (?<name>\w+) = (?<value>[0-9.]+);")]
    private static partial Regex ConstantPattern();

    [GeneratedRegex(@"^\s*(?:float4|float2|float|int)\s+(?<name>\w+)(?:\[\w+\])?\s*:\s*packoffset\(c(?<register>\d+)(?:\.(?<component>[xyzw]))?\);", RegexOptions.Multiline)]
    private static partial Regex MemberPattern();

    static float Constant(string name)
        => float.Parse(ConstantPattern().Matches(Source).Single(match => match.Groups["name"].Value == name).Groups["value"].Value, CultureInfo.InvariantCulture);

    [Fact]
    public void TheShaderAndTheEffectAgreeOnTheConstants()
    {
        Assert.Equal(Spectrum.MaxSteps, Constant("MaxSteps"));
        Assert.Equal(Spectrum.SpectralTableLength, Constant("TableSize"));
        Assert.Equal(Spectrum.Unroll, Constant("Unroll"));
        Assert.Equal((int)ShiftMode.Radial, Constant("ModeRadial"));
        Assert.Equal((int)ShiftMode.Rotational, Constant("ModeRotational"));
        Assert.Equal(ShiftGeometry.MaxRadialShift, Constant("MaxRadialShift"));
    }

    [Fact]
    public void TheConstantBufferMatchesTheShaderLayout()
    {
        var members = MemberPattern().Matches(Source)
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => int.Parse(match.Groups["register"].Value, CultureInfo.InvariantCulture) * 16
                    + (match.Groups["component"].Success ? "xyzw".IndexOf(match.Groups["component"].Value[0]) * 4 : 0));

        Assert.Equal(Fields.Keys.Order(), members.Keys.Order());
        Assert.All(members, member => Assert.Equal(member.Value, (int)Marshal.OffsetOf(ConstantBuffer, Fields[member.Key])));
    }

    [Fact]
    public void TheConstantBufferFillsWholeRegistersAndHoldsTheTable()
    {
        var size = (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(ConstantBuffer).Invoke(null, null)!;

        Assert.Equal(16 * (4 + Spectrum.SpectralTableLength), size);
    }
}
