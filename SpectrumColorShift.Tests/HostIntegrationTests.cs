using System.Windows;
using Telemetry;

namespace SpectrumColorShift.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        SpectrumColorShiftTelemetry.EnsureStartedOnce();
        SpectrumColorShiftTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new SpectrumColorShiftEffect();

        Assert.Equal(Texts.SpectrumColorShift, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
