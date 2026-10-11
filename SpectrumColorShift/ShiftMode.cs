using System.ComponentModel.DataAnnotations;

namespace SpectrumColorShift
{
    public enum ShiftMode
    {
        [Display(Name = nameof(Texts.ModeLinear), Description = nameof(Texts.ModeLinearDescription), ResourceType = typeof(Texts))]
        Linear = 1,

        [Display(Name = nameof(Texts.ModeRadial), Description = nameof(Texts.ModeRadialDescription), ResourceType = typeof(Texts))]
        Radial = 2,

        [Display(Name = nameof(Texts.ModeRotational), Description = nameof(Texts.ModeRotationalDescription), ResourceType = typeof(Texts))]
        Rotational = 4,
    }
}
