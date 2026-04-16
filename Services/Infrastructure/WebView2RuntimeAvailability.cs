using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using Microsoft.Web.WebView2.Core;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class WebView2RuntimeAvailability : IWebView2RuntimeAvailability
{
    public bool IsRuntimeInstalled()
    {
        try
        {
            var v = CoreWebView2Environment.GetAvailableBrowserVersionString(browserExecutableFolder: null);
            return !string.IsNullOrEmpty(v);
        }
        catch
        {
            return false;
        }
    }
}
