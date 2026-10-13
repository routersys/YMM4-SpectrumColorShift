using Vortice.Direct2D1;

namespace SpectrumColorShift;

internal static class ShaderVariants
{
    public static ShaderFamily Legacy { get; } = new("SpectrumColorShift", Enum.GetValues<ShiftMode>(), Spectrum.MinSteps, Spectrum.MaxSteps, Spectrum.StepQuantum);

    public static ShaderFamily Physical { get; } = new("SpectrumColorShiftPhysical", Enum.GetValues<ShiftMode>(), Spectrum.MinSteps, Spectrum.MaxSteps, Spectrum.StepQuantum);

    public static ShaderFamily Filter { get; } = new("SpectrumColorShiftPhysicalFilter", [ShiftMode.Linear], SpectralFilter.TapQuantum, SpectralFilter.MaxTaps, SpectralFilter.TapQuantum);

    public static byte[] DefaultBytes => Legacy.Bytes(ShiftMode.Linear, Spectrum.DefaultSteps);

    public static ShaderFamily Stepped(bool physical) => physical ? Physical : Legacy;

    public static void LoadAll(ID2D1EffectContext effectContext)
    {
        Legacy.LoadAll(effectContext);
        Physical.LoadAll(effectContext);
        Filter.LoadAll(effectContext);
    }
}
