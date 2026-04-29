#nullable enable

using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNeedleBitmapCache : INeedleBitmapCache
{
    private sealed record Entry(DateTime WriteTimeUtc, BitmapSource Bitmap);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public BitmapSource? GetOrLoadExistingFile(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        var fullPath = Path.GetFullPath(absolutePath);
        var stamp = File.GetLastWriteTimeUtc(fullPath);
        if (_entries.TryGetValue(fullPath, out var cached) && cached.WriteTimeUtc == stamp)
            return cached.Bitmap;

        var bitmap = LoadFrozen(fullPath);
        if (bitmap is null)
            return null;

        _entries[fullPath] = new Entry(stamp, bitmap);
        return bitmap;
    }

    private static BitmapSource? LoadFrozen(string fullPath)
    {
        try
        {
            using var fs = File.OpenRead(fullPath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = fs;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
