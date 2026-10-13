using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift;

internal static class StepShaders
{
    private const string LegacyName = "SpectrumColorShift";
    private const string PhysicalName = "SpectrumColorShiftPhysical";

    private static readonly Variants Legacy = new(LegacyName);
    private static readonly Variants Physical = new(PhysicalName);

    public static IEnumerable<int> Counts => Enumerable.Range(0, Variants.CountLength).Select(Variants.CountAt);

    public static IEnumerable<ShiftMode> Modes => Variants.Modes;

    public static byte[] DefaultBytes => Legacy.Bytes(ShiftMode.Linear, Spectrum.DefaultSteps);

    public static string ResourceName(bool physical, ShiftMode mode, int count) => (physical ? Physical : Legacy).ResourceName(mode, count);

    public static Guid Select(bool physical, ShiftMode mode, int steps) => (physical ? Physical : Legacy).Id(mode, steps);

    public static void Load(ID2D1EffectContext effectContext)
    {
        Legacy.Load(effectContext);
        Physical.Load(effectContext);
    }

    private sealed class Variants
    {
        public static ShiftMode[] Modes { get; } = Enum.GetValues<ShiftMode>();

        public static int CountLength { get; } = (Spectrum.MaxSteps - Spectrum.MinSteps) / Spectrum.StepQuantum + 1;

        private readonly string _name;
        private readonly Guid[] _ids;
        private readonly Lazy<byte[]>[] _bytes;

        public Variants(string name)
        {
            _name = name;
            _ids = [.. Enumerable.Range(0, Modes.Length * CountLength).Select(_ => Guid.NewGuid())];
            _bytes = [.. Enumerable.Range(0, Modes.Length * CountLength).Select(index => new Lazy<byte[]>(() => Read(index)))];
        }

        public static int CountAt(int index) => Spectrum.MinSteps + index * Spectrum.StepQuantum;

        public string ResourceName(ShiftMode mode, int count) => $"{_name}{mode}{count}";

        public Guid Id(ShiftMode mode, int steps) => _ids[IndexOf(mode, steps)];

        public byte[] Bytes(ShiftMode mode, int steps) => _bytes[IndexOf(mode, steps)].Value;

        public void Load(ID2D1EffectContext effectContext)
        {
            for (var index = 0; index < _ids.Length; index++)
            {
                var bytes = _bytes[index].Value;
                effectContext.LoadPixelShader(_ids[index], bytes, bytes.Length);
            }
        }

        private static int IndexOf(ShiftMode mode, int steps)
            => Array.IndexOf(Modes, mode) * CountLength + (Spectrum.Padded(steps) - Spectrum.MinSteps) / Spectrum.StepQuantum;

        private byte[] Read(int index)
            => PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(ResourceName(Modes[index / CountLength], CountAt(index % CountLength))));
    }
}
