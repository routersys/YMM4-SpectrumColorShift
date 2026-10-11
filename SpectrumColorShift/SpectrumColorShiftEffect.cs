using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace SpectrumColorShift
{
    [PluginDetails(AuthorName = "routersys")]
    [VideoEffect(nameof(Texts.SpectrumColorShift), [VideoEffectCategories.Filtering], [nameof(Texts.TagColorShift), nameof(Texts.TagChromaticAberration), nameof(Texts.TagSpectrum), nameof(Texts.TagDispersion), nameof(Texts.TagPrism), nameof(Texts.TagRgbSplit)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class SpectrumColorShiftEffect : VideoEffectBase
    {
        public SpectrumColorShiftEffect()
        {
            SpectrumColorShiftTelemetry.EnsureStartedOnce();
            SpectrumColorShiftUpdateNotifier.EnsureCheckedOnce();
        }

        public override string Label => Texts.SpectrumColorShift;

        [Display(GroupName = nameof(Texts.ShiftGroup), Name = nameof(Texts.Distance), Description = nameof(Texts.DistanceDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation Distance { get; } = new Animation(8, -4096, 4096);

        [Display(GroupName = nameof(Texts.ShiftGroup), Name = nameof(Texts.Mode), Description = nameof(Texts.ModeDescription), Order = 1, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ShiftMode Mode
        {
            get => _mode;
            set => Set(ref _mode, value);
        }
        private ShiftMode _mode = ShiftMode.Linear;

        [Display(GroupName = nameof(Texts.ShiftGroup), Name = nameof(Texts.Angle), Description = nameof(Texts.AngleDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        [ShowPropertyEditorWhen(nameof(Mode), ShiftMode.Linear)]
        public Animation Angle { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShiftGroup), Name = nameof(Texts.CenterX), Description = nameof(Texts.CenterXDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        [ShowPropertyEditorWhen(nameof(Mode), ShiftMode.Radial | ShiftMode.Rotational)]
        public Animation CenterX { get; } = new Animation(0, -4096, 4096);

        [Display(GroupName = nameof(Texts.ShiftGroup), Name = nameof(Texts.CenterY), Description = nameof(Texts.CenterYDescription), Order = 4, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        [ShowPropertyEditorWhen(nameof(Mode), ShiftMode.Radial | ShiftMode.Rotational)]
        public Animation CenterY { get; } = new Animation(0, -4096, 4096);

        [Display(GroupName = nameof(Texts.ShiftGroup), Name = nameof(Texts.Strength), Description = nameof(Texts.StrengthDescription), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.SpectrumGroup), Name = nameof(Texts.Dispersion), Description = nameof(Texts.DispersionDescription), Order = 10, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public DispersionLaw Dispersion
        {
            get => _dispersion;
            set => Set(ref _dispersion, value);
        }
        private DispersionLaw _dispersion = DispersionLaw.Grating;

        [Display(GroupName = nameof(Texts.SpectrumGroup), Name = nameof(Texts.ReferenceWavelength), Description = nameof(Texts.ReferenceWavelengthDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "nm", Spectrum.ShortestWavelength, Spectrum.LongestWavelength)]
        public Animation ReferenceWavelength { get; } = new Animation(Spectrum.DefaultReferenceWavelength, Spectrum.ShortestWavelength, Spectrum.LongestWavelength);

        [Display(GroupName = nameof(Texts.SpectrumGroup), Name = nameof(Texts.Steps), Description = nameof(Texts.StepsDescription), Order = 12, ResourceType = typeof(Texts))]
        [Range(Spectrum.MinSteps, Spectrum.MaxSteps)]
        [DefaultValue(Spectrum.DefaultSteps)]
        [TextBoxSlider("F0", "", Spectrum.MinSteps, Spectrum.MaxSteps)]
        public int Steps
        {
            get => _steps;
            set => Set(ref _steps, Spectrum.ClampSteps(value));
        }
        private int _steps = Spectrum.DefaultSteps;

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex,
            ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            try
            {
                return new SpectrumColorShiftEffectProcessor(devices, this);
            }
            catch (Exception exception)
            {
                SpectrumColorShiftTelemetry.Report(exception);
                throw;
            }
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Distance, Angle, CenterX, CenterY, Strength, ReferenceWavelength];
    }
}
