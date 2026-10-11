using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace SpectrumColorShift.Tests;

public sealed class SpectrumColorShiftEffectTests
{
    static PropertyInfo Property(string name) => typeof(SpectrumColorShiftEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Visibility VisibilityOf(string property, ShiftMode mode)
    {
        var attribute = Attribute<ShowPropertyEditorWhenAttribute>(property);
        var binding = attribute.GetBinding(new SpectrumColorShiftEffect { Mode = mode });

        return (Visibility)((System.Windows.Data.IValueConverter)binding.Converter).Convert(mode, typeof(Visibility), binding.ConverterParameter, CultureInfo.InvariantCulture);
    }

    [Theory]
    [InlineData(nameof(SpectrumColorShiftEffect.Distance), 8d, -4096d, 4096d)]
    [InlineData(nameof(SpectrumColorShiftEffect.Angle), 0d, -100000d, 100000d)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterX), 0d, -4096d, 4096d)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterY), 0d, -4096d, 4096d)]
    [InlineData(nameof(SpectrumColorShiftEffect.Strength), 100d, 0d, 100d)]
    [InlineData(nameof(SpectrumColorShiftEffect.ReferenceWavelength), 550d, 400d, 700d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new SpectrumColorShiftEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void TheModeTheDispersionAndTheStepsStartFromTheirDefaults()
    {
        var effect = new SpectrumColorShiftEffect();

        Assert.Equal(ShiftMode.Linear, effect.Mode);
        Assert.Equal(DispersionLaw.Grating, effect.Dispersion);
        Assert.Equal(32, effect.Steps);
    }

    [Theory]
    [InlineData(int.MinValue, 8)]
    [InlineData(0, 8)]
    [InlineData(7, 8)]
    [InlineData(8, 8)]
    [InlineData(32, 32)]
    [InlineData(63, 63)]
    [InlineData(64, 64)]
    [InlineData(65, 64)]
    [InlineData(int.MaxValue, 64)]
    public void TheStepsStayBetweenEightAndSixtyFour(int value, int expected)
    {
        var effect = new SpectrumColorShiftEffect { Steps = 20 };

        effect.Steps = value;

        Assert.Equal(expected, effect.Steps);
        Assert.False(effect.HasErrors);
    }

    [Fact]
    public void ChangingTheModeTheDispersionOrTheStepsNotifiesTheEditor()
    {
        var effect = new SpectrumColorShiftEffect();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.Mode = ShiftMode.Radial;
        effect.Dispersion = DispersionLaw.Prism;
        effect.Steps = 16;

        Assert.Equal([nameof(SpectrumColorShiftEffect.Mode), nameof(SpectrumColorShiftEffect.Dispersion), nameof(SpectrumColorShiftEffect.Steps)], changed);
    }

    [Fact]
    public void AssigningAnUnchangedOrClampedValueDoesNotNotify()
    {
        var effect = new SpectrumColorShiftEffect { Steps = 8 };
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.Mode = ShiftMode.Linear;
        effect.Dispersion = DispersionLaw.Grating;
        effect.Steps = 8;
        effect.Steps = 0;

        Assert.Empty(changed);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        var effect = new SpectrumColorShiftEffect();

        Assert.Equal(Texts.SpectrumColorShift, effect.Label);
    }

    [Fact]
    public void TheSixAnimatedParametersReceiveTheAnimationParameters()
    {
        var effect = new SpectrumColorShiftEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.All(
            [effect.Distance, effect.Angle, effect.CenterX, effect.CenterY, effect.Strength, effect.ReferenceWavelength],
            animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new SpectrumColorShiftEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheEffectIsRegisteredForFilteringWithoutAviUtlSupport()
    {
        var attribute = typeof(SpectrumColorShiftEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.SpectrumColorShift), attribute.Name);
        Assert.Equal([VideoEffectCategories.Filtering], attribute.Categories);
        Assert.Equal(
            [nameof(Texts.TagColorShift), nameof(Texts.TagChromaticAberration), nameof(Texts.TagSpectrum), nameof(Texts.TagDispersion), nameof(Texts.TagPrism), nameof(Texts.TagRgbSplit)],
            attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.SpectrumColorShift, attribute.GetName());
    }

    [Fact]
    public void TheAuthorIsDeclared()
    {
        var details = typeof(SpectrumColorShiftEffect).GetCustomAttribute<PluginDetailsAttribute>()!;

        Assert.Equal("routersys", details.AuthorName);
    }

    [Theory]
    [InlineData(nameof(SpectrumColorShiftEffect.Distance), nameof(Texts.ShiftGroup), nameof(Texts.Distance), nameof(Texts.DistanceDescription), 0)]
    [InlineData(nameof(SpectrumColorShiftEffect.Mode), nameof(Texts.ShiftGroup), nameof(Texts.Mode), nameof(Texts.ModeDescription), 1)]
    [InlineData(nameof(SpectrumColorShiftEffect.Angle), nameof(Texts.ShiftGroup), nameof(Texts.Angle), nameof(Texts.AngleDescription), 2)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterX), nameof(Texts.ShiftGroup), nameof(Texts.CenterX), nameof(Texts.CenterXDescription), 3)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterY), nameof(Texts.ShiftGroup), nameof(Texts.CenterY), nameof(Texts.CenterYDescription), 4)]
    [InlineData(nameof(SpectrumColorShiftEffect.Strength), nameof(Texts.ShiftGroup), nameof(Texts.Strength), nameof(Texts.StrengthDescription), 5)]
    [InlineData(nameof(SpectrumColorShiftEffect.Dispersion), nameof(Texts.SpectrumGroup), nameof(Texts.Dispersion), nameof(Texts.DispersionDescription), 10)]
    [InlineData(nameof(SpectrumColorShiftEffect.ReferenceWavelength), nameof(Texts.SpectrumGroup), nameof(Texts.ReferenceWavelength), nameof(Texts.ReferenceWavelengthDescription), 11)]
    [InlineData(nameof(SpectrumColorShiftEffect.Steps), nameof(Texts.SpectrumGroup), nameof(Texts.Steps), nameof(Texts.StepsDescription), 12)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(SpectrumColorShiftEffect.Distance), "F1", "px", -100d, 100d)]
    [InlineData(nameof(SpectrumColorShiftEffect.Angle), "F1", "°", -360d, 360d)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterX), "F1", "px", -500d, 500d)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterY), "F1", "px", -500d, 500d)]
    [InlineData(nameof(SpectrumColorShiftEffect.Strength), "F1", "%", 0d, 100d)]
    [InlineData(nameof(SpectrumColorShiftEffect.ReferenceWavelength), "F0", "nm", 400d, 700d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void TheStepsAreEditedWithoutAUnitFromEightToSixtyFour()
    {
        var slider = Attribute<TextBoxSliderAttribute>(nameof(SpectrumColorShiftEffect.Steps));
        var range = Attribute<RangeAttribute>(nameof(SpectrumColorShiftEffect.Steps));

        Assert.Equal("F0", slider.StringFormat);
        Assert.Equal(string.Empty, slider.UnitText);
        Assert.Equal(8d, slider.DefaultMin);
        Assert.Equal(64d, slider.DefaultMax);
        Assert.Equal(8, range.Minimum);
        Assert.Equal(64, range.Maximum);
        Assert.Equal(32, Attribute<DefaultValueAttribute>(nameof(SpectrumColorShiftEffect.Steps)).Value);
    }

    [Theory]
    [InlineData(ShiftMode.Linear, Visibility.Visible)]
    [InlineData(ShiftMode.Radial, Visibility.Collapsed)]
    [InlineData(ShiftMode.Rotational, Visibility.Collapsed)]
    public void TheAngleIsShownOnlyForTheLinearMode(ShiftMode mode, Visibility expected)
    {
        Assert.Equal(expected, VisibilityOf(nameof(SpectrumColorShiftEffect.Angle), mode));
    }

    [Theory]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterX), ShiftMode.Linear, Visibility.Collapsed)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterX), ShiftMode.Radial, Visibility.Visible)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterX), ShiftMode.Rotational, Visibility.Visible)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterY), ShiftMode.Linear, Visibility.Collapsed)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterY), ShiftMode.Radial, Visibility.Visible)]
    [InlineData(nameof(SpectrumColorShiftEffect.CenterY), ShiftMode.Rotational, Visibility.Visible)]
    public void TheCenterIsShownOnlyForTheRadialAndRotationalModes(string property, ShiftMode mode, Visibility expected)
    {
        Assert.Equal(expected, VisibilityOf(property, mode));
    }

    [Fact]
    public void TheStoredValuesOfTheEnumerationsDoNotChange()
    {
        Assert.Equal([1, 2, 4], Enum.GetValues<ShiftMode>().Select(value => (int)value));
        Assert.Equal([0, 1], Enum.GetValues<DispersionLaw>().Select(value => (int)value));
    }

    [Theory]
    [InlineData(ShiftMode.Linear, nameof(Texts.ModeLinear), nameof(Texts.ModeLinearDescription))]
    [InlineData(ShiftMode.Radial, nameof(Texts.ModeRadial), nameof(Texts.ModeRadialDescription))]
    [InlineData(ShiftMode.Rotational, nameof(Texts.ModeRotational), nameof(Texts.ModeRotationalDescription))]
    public void EveryShiftModeIsDescribed(ShiftMode mode, string name, string description)
    {
        var display = typeof(ShiftMode).GetField(mode.ToString())!.GetCustomAttribute<DisplayAttribute>()!;

        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(DispersionLaw.Grating, nameof(Texts.DispersionGrating), nameof(Texts.DispersionGratingDescription))]
    [InlineData(DispersionLaw.Prism, nameof(Texts.DispersionPrism), nameof(Texts.DispersionPrismDescription))]
    public void EveryDispersionLawIsDescribed(DispersionLaw law, string name, string description)
    {
        var display = typeof(DispersionLaw).GetField(law.ToString())!.GetCustomAttribute<DisplayAttribute>()!;

        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new SpectrumColorShiftEffect { Mode = ShiftMode.Rotational, Dispersion = DispersionLaw.Prism, Steps = 48 };
        effect.Distance.Values[0].Value = -12.5d;
        effect.Angle.Values[0].Value = 135d;
        effect.CenterX.Values[0].Value = 80d;
        effect.CenterY.Values[0].Value = -45d;
        effect.Strength.Values[0].Value = 60d;
        effect.ReferenceWavelength.Values[0].Value = 620d;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(ShiftMode.Rotational, clone.Mode);
        Assert.Equal(DispersionLaw.Prism, clone.Dispersion);
        Assert.Equal(48, clone.Steps);
        Assert.Equal(-12.5d, clone.Distance.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(135d, clone.Angle.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(80d, clone.CenterX.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(-45d, clone.CenterY.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(60d, clone.Strength.GetValue(0, 1, EffectDescriptions.Fps));
        Assert.Equal(620d, clone.ReferenceWavelength.GetValue(0, 1, EffectDescriptions.Fps));
    }
}
