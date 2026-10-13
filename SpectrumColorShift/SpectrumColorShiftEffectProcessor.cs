using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace SpectrumColorShift;

internal sealed class SpectrumColorShiftEffectProcessor(
    IGraphicsDevicesAndContext devices,
    SpectrumColorShiftEffect item) : VideoEffectProcessorBase(devices)
{
    private readonly SpectrumColorShiftEffect _item = item;
    private SpectrumColorShiftCustomEffect? _effect;

    private bool _isFirst = true;
    private Parameters _parameters;

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            SpectrumColorShiftTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _effect is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        var parameters = new Parameters(
            (float)_item.Distance.GetValue(frame, length, fps),
            (int)_item.Mode,
            (float)_item.Angle.GetValue(frame, length, fps),
            (float)_item.CenterX.GetValue(frame, length, fps),
            (float)_item.CenterY.GetValue(frame, length, fps),
            (float)(_item.Strength.GetValue(frame, length, fps) / 100.0),
            (int)_item.Dispersion,
            (float)_item.ReferenceWavelength.GetValue(frame, length, fps),
            _item.Steps,
            _item.Physical);

        if (_isFirst || _parameters.Distance != parameters.Distance)
            _effect.Distance = parameters.Distance;
        if (_isFirst || _parameters.Mode != parameters.Mode)
            _effect.Mode = parameters.Mode;
        if (_isFirst || _parameters.Angle != parameters.Angle)
            _effect.Angle = parameters.Angle;
        if (_isFirst || _parameters.CenterX != parameters.CenterX)
            _effect.CenterX = parameters.CenterX;
        if (_isFirst || _parameters.CenterY != parameters.CenterY)
            _effect.CenterY = parameters.CenterY;
        if (_isFirst || _parameters.Strength != parameters.Strength)
            _effect.Strength = parameters.Strength;
        if (_isFirst || _parameters.Dispersion != parameters.Dispersion)
            _effect.Dispersion = parameters.Dispersion;
        if (_isFirst || _parameters.ReferenceWavelength != parameters.ReferenceWavelength)
            _effect.ReferenceWavelength = parameters.ReferenceWavelength;
        if (_isFirst || _parameters.Steps != parameters.Steps)
            _effect.Steps = parameters.Steps;
        if (_isFirst || _parameters.Physical != parameters.Physical)
            _effect.Physical = parameters.Physical ? 1 : 0;

        _parameters = parameters;
        _isFirst = false;

        return effectDescription.DrawDescription;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        _effect = new SpectrumColorShiftCustomEffect(devices);
        if (!_effect.IsEnabled)
        {
            _effect.Dispose();
            _effect = null;
            return null;
        }
        disposer.Collect(_effect);

        var output = _effect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _effect?.SetSource(input, true);
    }

    protected override void ClearEffectChain()
    {
        try
        {
            ClearEffectChainCore();
        }
        catch (Exception exception)
        {
            SpectrumColorShiftTelemetry.Report(exception);
            throw;
        }
    }

    private void ClearEffectChainCore()
    {
        _effect?.SetSource(null, true);
        _isFirst = true;
    }

    private readonly record struct Parameters(
        float Distance,
        int Mode,
        float Angle,
        float CenterX,
        float CenterY,
        float Strength,
        int Dispersion,
        float ReferenceWavelength,
        int Steps,
        bool Physical);
}
