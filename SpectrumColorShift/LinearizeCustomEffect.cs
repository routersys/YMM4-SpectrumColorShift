using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SpectrumColorShift;

internal sealed class LinearizeCustomEffect : D2D1CustomShaderEffectBase
{
    public LinearizeCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices))
    {
        if (IsEnabled)
            SetValue((int)Property.Precision, BufferPrecision.PerChannel16Float);
    }

    [CustomEffect(1)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        public EffectImpl() : base(ShaderResourceUri.Get("SpectrumColorShiftLinearize"))
        {
        }

        protected override void UpdateConstants()
        {
        }
    }
}
