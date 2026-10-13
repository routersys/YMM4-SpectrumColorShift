using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SpectrumColorShift.Tests;

internal static partial class ConstantBufferContract
{
    [GeneratedRegex(@"^\s*(?:float4|float2|float|int)\s+(?<name>\w+)(?:\[\w+\])?\s*:\s*packoffset\(c(?<register>\d+)(?:\.(?<component>[xyzw]))?\);", RegexOptions.Multiline)]
    private static partial Regex MemberPattern();

    public static IReadOnlyDictionary<string, int> Declared(string source)
        => MemberPattern().Matches(source).ToDictionary(
            match => match.Groups["name"].Value,
            match => int.Parse(match.Groups["register"].Value) * 16 + (match.Groups["component"].Success ? "xyzw".IndexOf(match.Groups["component"].Value[0]) * 4 : 0));

    public static int OffsetOf(Type type, string path)
    {
        var offset = 0;
        foreach (var name in path.Split('.'))
        {
            offset += (int)Marshal.OffsetOf(type, name);
            type = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.FieldType;
        }

        return offset;
    }

    public static int SizeOf(Type type) => (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null, null)!;
}
