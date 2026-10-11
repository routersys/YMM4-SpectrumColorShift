using System.ComponentModel.DataAnnotations;

namespace SpectrumColorShift
{
    public enum DispersionLaw
    {
        [Display(Name = nameof(Texts.DispersionGrating), Description = nameof(Texts.DispersionGratingDescription), ResourceType = typeof(Texts))]
        Grating = 0,

        [Display(Name = nameof(Texts.DispersionPrism), Description = nameof(Texts.DispersionPrismDescription), ResourceType = typeof(Texts))]
        Prism = 1,
    }
}
