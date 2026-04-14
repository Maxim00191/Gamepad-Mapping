using System;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Register global handlers as early as possible
        StartupDiagnostics.RegisterHandlers(App.Logger);

        var app = new App();

        try
        {
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            App.Logger.Error("Fatal exception during application lifecycle", ex);
            StartupDiagnostics.ShowFatalErrorDialog(ex);
        }
    }
}
