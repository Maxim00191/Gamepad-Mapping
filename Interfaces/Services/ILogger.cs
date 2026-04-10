namespace GamepadMapperGUI.Interfaces.Services;

public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
    void Debug(string message) => Log(LogLevel.Debug, message);
    void Info(string message) => Log(LogLevel.Info, message);
    void Warning(string message) => Log(LogLevel.Warning, message);
    void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
}
