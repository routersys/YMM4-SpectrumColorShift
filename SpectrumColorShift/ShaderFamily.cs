using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace SpectrumColorShift;

internal sealed class ShaderFamily
{
    private readonly string _name;
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _quantum;
    private readonly int _length;
    private readonly Guid[] _ids;
    private readonly Lazy<byte[]>[] _bytes;

    public ShaderFamily(string name, IReadOnlyList<ShiftMode> modes, int minimum, int maximum, int quantum)
    {
        _name = name;
        Modes = modes;
        _minimum = minimum;
        _maximum = maximum;
        _quantum = quantum;
        _length = (maximum - minimum) / quantum + 1;
        _ids = [.. Enumerable.Range(0, modes.Count * _length).Select(_ => Guid.NewGuid())];
        _bytes = [.. Enumerable.Range(0, modes.Count * _length).Select(index => new Lazy<byte[]>(() => Read(index)))];
    }

    public IReadOnlyList<ShiftMode> Modes { get; }

    public IEnumerable<int> Counts => Enumerable.Range(0, _length).Select(CountAt);

    public string ResourceName(ShiftMode mode, int count) => $"{_name}{mode}{count}";

    public Guid Id(ShiftMode mode, int count) => _ids[IndexOf(mode, count)];

    public byte[] Bytes(ShiftMode mode, int count) => _bytes[IndexOf(mode, count)].Value;

    public void LoadAll(ID2D1EffectContext effectContext)
    {
        for (var index = 0; index < _ids.Length; index++)
        {
            var bytes = _bytes[index].Value;
            effectContext.LoadPixelShader(_ids[index], bytes, bytes.Length);
        }
    }

    private int CountAt(int index) => _minimum + index * _quantum;

    private int IndexOf(ShiftMode mode, int count)
    {
        var offset = Quantum.RoundUp(Math.Clamp(count, _minimum, _maximum) - _minimum, _quantum) / _quantum;

        return IndexOfMode(mode) * _length + offset;
    }

    private int IndexOfMode(ShiftMode mode)
    {
        for (var index = 0; index < Modes.Count; index++)
        {
            if (Modes[index] == mode)
                return index;
        }

        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
    }

    private byte[] Read(int index)
        => PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(ResourceName(Modes[index / _length], CountAt(index % _length))));
}
