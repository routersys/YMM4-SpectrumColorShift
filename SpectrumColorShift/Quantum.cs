namespace SpectrumColorShift;

internal static class Quantum
{
    public static int RoundUp(int value, int quantum) => (value + quantum - 1) / quantum * quantum;
}
