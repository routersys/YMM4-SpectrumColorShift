namespace SpectrumColorShift.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, SpectrumColorShiftEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("distance-0", Create(effect => effect.Distance.Values[0].Value = 0), [0]);
        yield return ("distance-24", Create(effect => effect.Distance.Values[0].Value = 24), [0]);
        yield return ("distance-minus-8", Create(effect => effect.Distance.Values[0].Value = -8), [0]);
        yield return ("distance-100-steps-64", Create(effect =>
        {
            effect.Distance.Values[0].Value = 100;
            effect.Steps = 64;
        }), [0]);
        yield return ("angle-45", Create(effect => effect.Angle.Values[0].Value = 45), [0]);
        yield return ("angle-90", Create(effect => effect.Angle.Values[0].Value = 90), [0]);
        yield return ("radial-24", Create(effect =>
        {
            effect.Mode = ShiftMode.Radial;
            effect.Distance.Values[0].Value = 24;
        }), [0]);
        yield return ("radial-center-offset", Create(effect =>
        {
            effect.Mode = ShiftMode.Radial;
            effect.Distance.Values[0].Value = 24;
            effect.CenterX.Values[0].Value = 120;
            effect.CenterY.Values[0].Value = -60;
        }), [0]);
        yield return ("rotational-24", Create(effect =>
        {
            effect.Mode = ShiftMode.Rotational;
            effect.Distance.Values[0].Value = 24;
        }), [0]);
        yield return ("rotational-center-offset", Create(effect =>
        {
            effect.Mode = ShiftMode.Rotational;
            effect.Distance.Values[0].Value = 24;
            effect.CenterX.Values[0].Value = 120;
            effect.CenterY.Values[0].Value = -60;
        }), [0]);
        yield return ("prism", Create(effect => effect.Dispersion = DispersionLaw.Prism), [0]);
        yield return ("reference-450", Create(effect => effect.ReferenceWavelength.Values[0].Value = 450), [0]);
        yield return ("reference-650", Create(effect => effect.ReferenceWavelength.Values[0].Value = 650), [0]);
        yield return ("steps-8", Create(effect => effect.Steps = 8), [0]);
        yield return ("strength-50", Create(effect => effect.Strength.Values[0].Value = 50), [0]);
    }

    public static IEnumerable<(string Name, Func<SpectrumColorShiftEffect> Create, Action<SpectrumColorShiftEffect> Change, int Frame)> Transitions()
    {
        yield return ("distance-8-to-24", () => Create(), effect => effect.Distance.Values[0].Value = 24, 0);
        yield return ("distance-8-to-0", () => Create(), effect => effect.Distance.Values[0].Value = 0, 0);
        yield return ("distance-0-to-8", () => Create(effect => effect.Distance.Values[0].Value = 0), effect => effect.Distance.Values[0].Value = 8, 0);
        yield return ("mode-linear-to-radial", () => Create(), effect => effect.Mode = ShiftMode.Radial, 0);
        yield return ("mode-radial-to-rotational", () => Create(effect => effect.Mode = ShiftMode.Radial), effect => effect.Mode = ShiftMode.Rotational, 0);
        yield return ("angle-0-to-90", () => Create(), effect => effect.Angle.Values[0].Value = 90, 0);
        yield return ("center-0-to-offset", () => Create(effect => effect.Mode = ShiftMode.Radial), effect => effect.CenterX.Values[0].Value = 120, 0);
        yield return ("strength-100-to-50", () => Create(), effect => effect.Strength.Values[0].Value = 50, 0);
        yield return ("dispersion-grating-to-prism", () => Create(), effect => effect.Dispersion = DispersionLaw.Prism, 0);
        yield return ("reference-550-to-450", () => Create(), effect => effect.ReferenceWavelength.Values[0].Value = 450, 0);
        yield return ("steps-32-to-8", () => Create(), effect => effect.Steps = 8, 0);
        yield return ("steps-8-to-64", () => Create(effect => effect.Steps = 8), effect => effect.Steps = 64, 0);
    }

    public static IEnumerable<(string Name, SpectrumColorShiftEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("steps-64", Create(effect => effect.Steps = 64));
        yield return ("radial-24", Create(effect =>
        {
            effect.Mode = ShiftMode.Radial;
            effect.Distance.Values[0].Value = 24;
        }));
        yield return ("rotational-24", Create(effect =>
        {
            effect.Mode = ShiftMode.Rotational;
            effect.Distance.Values[0].Value = 24;
        }));
    }

    static SpectrumColorShiftEffect Create(Action<SpectrumColorShiftEffect>? configure = null)
    {
        var effect = new SpectrumColorShiftEffect();
        configure?.Invoke(effect);
        return effect;
    }
}
