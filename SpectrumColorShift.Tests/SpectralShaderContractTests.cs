using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static SpectrumColorShift.SpectrumColorShiftCustomEffect;

namespace SpectrumColorShift.Tests;

public sealed partial class SpectralShaderContractTests
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
        ["filterOrigin"] = "FilterOrigin",
        ["filterVertical"] = "FilterVertical",
        ["samples"] = "Samples",
        ["taps"] = "Taps",
    };

    static string Source { get; } = ShaderSource("Physical.hlsli");

    static string SrgbSource { get; } = ShaderSource("Srgb.hlsli");

    static string ShaderSource(string name) => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "SpectrumColorShift.Shader", name));

    static string Here([CallerFilePath] string path = "") => path;

    static int LinearInput { get; } = (int)typeof(SpectrumColorShiftCustomEffect).GetField("LinearInput", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

    static Type ConstantBuffer => typeof(EffectImpl).GetNestedType("PhysicalConstantBuffer", BindingFlags.NonPublic)!;

    [GeneratedRegex(@"static const (?:int|float) (?<name>\w+) = (?<value>[0-9.]+);")]
    private static partial Regex ConstantPattern();

    [GeneratedRegex(@"Texture2D\s+LinearTexture\s*:\s*register\(t(?<register>\d+)\)")]
    private static partial Regex TexturePattern();

    [GeneratedRegex(@"float4\s+\w+\s*:\s*TEXCOORD(?<index>\d+)")]
    private static partial Regex CoordinatePattern();

    static float Constant(string name) => Constant(Source, name);

    static float Constant(string source, string name)
        => float.Parse(ConstantPattern().Matches(source).Single(match => match.Groups["name"].Value == name).Groups["value"].Value, CultureInfo.InvariantCulture);

    [Fact]
    public void TheShaderAndTheEffectAgreeOnTheConstants()
    {
        Assert.Equal(Spectrum.MaxSteps, Constant("MaxSteps"));
        Assert.Equal(Spectrum.SpectralTableLength, Constant("TableSize"));
        Assert.Equal(SpectralFilter.TableLength, Constant("FilterTableSize"));
        Assert.Equal((int)ShiftMode.Radial, Constant("ModeRadial"));
        Assert.Equal((int)ShiftMode.Rotational, Constant("ModeRotational"));
        Assert.Equal(ShiftGeometry.MaxRadialShift, Constant("MaxRadialShift"));
    }

    [Fact]
    public void TheShaderReadsTheTextureOfTheLinearizedInput()
    {
        Assert.Equal(LinearInput, int.Parse(TexturePattern().Match(Source).Groups["register"].Value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TheShaderDeclaresTheTextureCoordinateOfEveryInputUpToTheOneItReads()
    {
        var declared = CoordinatePattern().Matches(Source).Select(match => int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture));

        Assert.Equal(Enumerable.Range(0, LinearInput + 1), declared.Order());
    }

    [Theory]
    [InlineData("DecodeThreshold", 0.04045f)]
    [InlineData("EncodeThreshold", 0.0031308f)]
    [InlineData("LowSlope", 12.92f)]
    [InlineData("Offset", 0.055f)]
    [InlineData("Scale", 1.055f)]
    [InlineData("Exponent", 2.4f)]
    public void TheSrgbHeaderHoldsTheStandardTransferConstants(string name, float expected)
        => Assert.Equal(expected, Constant(SrgbSource, name));

    [Fact]
    public void TheSrgbCurvesMeetAtTheThresholds()
    {
        var slope = Constant(SrgbSource, "LowSlope");
        var linearEnd = Constant(SrgbSource, "EncodeThreshold");
        var power = Constant(SrgbSource, "Scale") * MathF.Pow(linearEnd, 1f / Constant(SrgbSource, "Exponent")) - Constant(SrgbSource, "Offset");

        Assert.Equal(Constant(SrgbSource, "DecodeThreshold"), slope * linearEnd, 1e-5f);
        Assert.Equal(slope * linearEnd, power, 1e-5f);
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
        Assert.Equal(16 * (4 + Spectrum.SpectralTableLength + SpectralFilter.TableLength), ConstantBufferContract.SizeOf(ConstantBuffer));
    }
}
