using System.Windows.Media.Imaging;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationTemplateMatcher
{
    AutomationTemplateMatchResult Match(
        BitmapSource haystack,
        BitmapSource needle,
        AutomationImageProbeOptions options,
        CancellationToken cancellationToken = default);
}
