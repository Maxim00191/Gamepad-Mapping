using System.Reflection;

namespace GamepadMapperGUI.Utils;

public static class ExceptionMessageFormatter
{
    public static string UserFacingMessage(Exception? ex)
    {
        if (ex is null)
            return string.Empty;

        var current = ex;
        while (current is TargetInvocationException { InnerException: { } ti })
            current = ti;

        if (current is AggregateException agg)
        {
            var flat = agg.Flatten();
            if (flat.InnerExceptions.Count == 1)
                current = flat.InnerExceptions[0];
        }

        return string.IsNullOrWhiteSpace(current.Message)
            ? current.GetType().FullName ?? current.GetType().Name
            : current.Message;
    }
}
