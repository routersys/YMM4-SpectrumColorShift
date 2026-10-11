namespace SpectrumColorShift;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/SpectrumColorShift;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}
