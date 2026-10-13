using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift;

internal static class StepShaders
{
    private const string LegacyName = "SpectrumColorShift";
    private const string PhysicalName = "SpectrumColorShiftPhysical";

    private static readonly Variants Legacy = new(LegacyName);
    private static readonly Variants Physical = new(PhysicalName);

    public static IEnumerable<int> Counts => Enumerable.Range(0, Variants.Length).Select(CountAt);

    public static byte[] DefaultBytes => Legacy.Bytes(Spectrum.DefaultSteps);

    public static string ResourceName(bool physical, int count) => (physical ? Physical : Legacy).ResourceName(count);

    public static Guid Select(bool physical, int steps) => (physical ? Physical : Legacy).Id(steps);

    public static void Load(ID2D1EffectContext effectContext)
    {
        Legacy.Load(effectContext);
        Physical.Load(effectContext);
    }

    private static int CountAt(int index) => Spectrum.MinSteps + index * Spectrum.StepQuantum;

    private sealed class Variants(string name)
    {
        public static int Length { get; } = (Spectrum.MaxSteps - Spectrum.MinSteps) / Spectrum.StepQuantum + 1;

        private readonly Guid[] _ids = [.. Enumerable.Range(0, Length).Select(_ => Guid.NewGuid())];
        private readonly Lazy<byte[]>[] _bytes = [.. Enumerable.Range(0, Length).Select(index => new Lazy<byte[]>(() => Read(name, CountAt(index))))];

        public string ResourceName(int count) => $"{name}{count}";

        public Guid Id(int steps) => _ids[IndexOf(steps)];

        public byte[] Bytes(int steps) => _bytes[IndexOf(steps)].Value;

        public void Load(ID2D1EffectContext effectContext)
        {
            for (var index = 0; index < Length; index++)
            {
                var bytes = _bytes[index].Value;
                effectContext.LoadPixelShader(_ids[index], bytes, bytes.Length);
            }
        }

        private static int IndexOf(int steps) => (Spectrum.Padded(steps) - Spectrum.MinSteps) / Spectrum.StepQuantum;

        private static byte[] Read(string name, int count) => PackResourceReader.ReadAllBytes(ShaderResourceUri.Get($"{name}{count}"));
    }
}
