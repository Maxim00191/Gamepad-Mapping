using GamepadMapperGUI.Services.Infrastructure;

namespace GamepadMapping.Tests.Services.Infrastructure;

public sealed class MainShellVisibilityTests
{
    [Fact]
    public void NotifyHiddenThenShown_TogglesAndRaisesEvents()
    {
        var shell = new MainShellVisibility();
        var raised = 0;
        shell.PrimaryShellHiddenToTrayChanged += (_, _) => raised++;

        shell.NotifyPrimaryShellHiddenToTray();
        Assert.True(shell.IsPrimaryShellHiddenToTray);
        Assert.Equal(1, raised);

        shell.NotifyPrimaryShellHiddenToTray();
        Assert.Equal(1, raised);

        shell.NotifyPrimaryShellShownFromTray();
        Assert.False(shell.IsPrimaryShellHiddenToTray);
        Assert.Equal(2, raised);

        shell.NotifyPrimaryShellShownFromTray();
        Assert.Equal(2, raised);
    }
}
