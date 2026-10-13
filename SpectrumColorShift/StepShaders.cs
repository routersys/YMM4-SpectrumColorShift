using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift;

internal static class StepShaders
{
    private const string LegacyName = "SpectrumColorShift";
    private const string PhysicalName = "SpectrumColorShiftPhysical";
    private const string FilterName = "SpectrumColorShiftPhysicalFilter";

    private static readonly ShiftMode[] AllModes = Enum.GetValues<ShiftMode>();

    private static readonly Variants Legacy = new(LegacyName, AllModes, Spectrum.MinSteps, Spectrum.MaxSteps, Spectrum.StepQuantum);
    private static readonly Variants Physical = new(PhysicalName, AllModes, Spectrum.MinSteps, Spectrum.MaxSteps, Spectrum.StepQuantum);
    private static readonly Variants Filter = new(FilterName, [ShiftMode.Linear], SpectralFilter.TapQuantum, SpectralFilter.MaxTaps, SpectralFilter.TapQuantum);

    public static IEnumerable<int> Counts => Legacy.Counts;

    public static IEnumerable<int> FilterCounts => Filter.Counts;

    public static IEnumerable<ShiftMode> Modes => AllModes;

    public static byte[] DefaultBytes => Legacy.Bytes(ShiftMode.Linear, Spectrum.DefaultSteps);

    public static string ResourceName(bool physical, ShiftMode mode, int count) => (physical ? Physical : Legacy).ResourceName(mode, count);

    public static string FilterResourceName(int taps) => Filter.ResourceName(ShiftMode.Linear, taps);

    public static Guid Select(bool physical, ShiftMode mode, int steps) => (physical ? Physical : Legacy).Id(mode, steps);

    public static Guid SelectFilter(int taps) => Filter.Id(ShiftMode.Linear, taps);

    public static void Load(ID2D1EffectContext effectContext)
    {
        Legacy.Load(effectContext);
        Physical.Load(effectContext);
        Filter.Load(effectContext);
    }

    private sealed class Variants
    {
        private readonly string _name;
        private readonly ShiftMode[] _modes;
        private readonly int _minimum;
        private readonly int _maximum;
        private readonly int _quantum;
        private readonly int _length;
        private readonly Guid[] _ids;
        private readonly Lazy<byte[]>[] _bytes;

        public Variants(string name, ShiftMode[] modes, int minimum, int maximum, int quantum)
        {
            _name = name;
            _modes = modes;
            _minimum = minimum;
            _maximum = maximum;
            _quantum = quantum;
            _length = (maximum - minimum) / quantum + 1;
            _ids = [.. Enumerable.Range(0, modes.Length * _length).Select(_ => Guid.NewGuid())];
            _bytes = [.. Enumerable.Range(0, modes.Length * _length).Select(index => new Lazy<byte[]>(() => Read(index)))];
        }

        public IEnumerable<int> Counts => Enumerable.Range(0, _length).Select(CountAt);

        public string ResourceName(ShiftMode mode, int count) => $"{_name}{mode}{count}";

        public Guid Id(ShiftMode mode, int count) => _ids[IndexOf(mode, count)];

        public byte[] Bytes(ShiftMode mode, int count) => _bytes[IndexOf(mode, count)].Value;

        public void Load(ID2D1EffectContext effectContext)
        {
            for (var index = 0; index < _ids.Length; index++)
            {
                var bytes = _bytes[index].Value;
                effectContext.LoadPixelShader(_ids[index], bytes, bytes.Length);
            }
        }

        private int CountAt(int index) => _minimum + index * _quantum;

        private int IndexOf(ShiftMode mode, int count)
            => Array.IndexOf(_modes, mode) * _length + (Math.Clamp(count, _minimum, _maximum) - _minimum + _quantum - 1) / _quantum;

        private byte[] Read(int index)
            => PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(ResourceName(_modes[index / _length], CountAt(index % _length))));
    }
}
