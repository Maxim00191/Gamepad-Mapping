using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Gamepad_Mapping.Behaviors;

namespace GamepadMapping.Tests.Behaviors;

public class ButtonAttentionAssistTests
{
    [Fact]
    public void Button_SoftWarningHighlight_DefaultsToFalse() =>
        RunSta(() =>
        {
            var button = new Button();
            Assert.False(ButtonAttentionAssist.GetIsSoftWarningHighlighted(button));
        });

    [Fact]
    public void ToggleButton_SoftWarningHighlight_CanBeToggled() =>
        RunSta(() =>
        {
            var button = new ToggleButton();
            ButtonAttentionAssist.SetIsSoftWarningHighlighted(button, true);
            Assert.True(ButtonAttentionAssist.GetIsSoftWarningHighlighted(button));

            ButtonAttentionAssist.SetIsSoftWarningHighlighted(button, false);
            Assert.False(ButtonAttentionAssist.GetIsSoftWarningHighlighted(button));
        });

    private static void RunSta(Action action)
    {
        Exception? caught = null;
        var t = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (caught is not null)
            throw caught;
    }
}
