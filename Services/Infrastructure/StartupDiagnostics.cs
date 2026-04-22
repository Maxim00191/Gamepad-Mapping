using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Provides global exception handling and startup diagnostics for the application.
/// </summary>
public static class StartupDiagnostics
{
    private static ILogger? _logger;
    private static string? _startupId;

    /// <summary>
    /// Registers global exception handlers for the application.
    /// </summary>
    public static void RegisterHandlers(ILogger logger)
    {
        _logger = logger;
        _startupId = Guid.NewGuid().ToString("D").Substring(0, 8);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (Application.Current != null)
        {
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        LogStartupEnvironment();
    }

    private static void LogStartupEnvironment()
    {
        if (_logger == null) return;

        _logger.Info($"[Startup:{_startupId}] Process started.");
        _logger.Info($"[Startup:{_startupId}] OS: {Environment.OSVersion}");
        _logger.Info($"[Startup:{_startupId}] Runtime: {Environment.Version} ({(Environment.Is64BitProcess ? "64-bit" : "32-bit")})");
        _logger.Info($"[Startup:{_startupId}] Base Directory: {AppContext.BaseDirectory}");
        _logger.Info($"[Startup:{_startupId}] Process Path: {Environment.ProcessPath}");
        _logger.Info($"[Startup:{_startupId}] Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var message = $"[Startup:{_startupId}] AppDomain Unhandled Exception (IsTerminating: {e.IsTerminating})";
            _logger?.Log(LogLevel.Error, message, ex);

            if (e.IsTerminating)
            {
                ShowFatalErrorDialog(ex);
            }
        }
        else
        {
            _logger?.Log(LogLevel.Error, $"[Startup:{_startupId}] AppDomain Unhandled Exception (Non-Exception object: {e.ExceptionObject})");
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Log(LogLevel.Error, $"[Startup:{_startupId}] UI Dispatcher Unhandled Exception", e.Exception);
        
        // We don't set e.Handled = true here by default to ensure the app crashes consistently 
        // if it's a truly unhandled exception, unless we want to keep it alive.
        // For now, let it crash but log it.
        ShowFatalErrorDialog(e.Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Log(LogLevel.Error, $"[Startup:{_startupId}] Unobserved Task Exception", e.Exception);
        // Usually we don't want to crash on unobserved task exceptions in .NET 4.5+, 
        // but we definitely want to log them.
        e.SetObserved();
    }

    public static void ShowFatalErrorDialog(Exception? ex)
    {
        var title = AppUiLocalization.GetString("StartupDiagnostics_FatalErrorTitle");
        var detail = string.IsNullOrWhiteSpace(ex?.Message)
            ? AppUiLocalization.GetString("StartupDiagnostics_UnknownError")
            : ex!.Message;
        var errorMessage = string.Format(System.Globalization.CultureInfo.CurrentUICulture,
            AppUiLocalization.GetString("StartupDiagnostics_FatalErrorBodyFormat"), detail);

        var dialogService = new UserDialogService();
        dialogService.ShowError(errorMessage, title);
    }
}
