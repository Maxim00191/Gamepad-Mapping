using System;

namespace GamepadMapperGUI.Models.Core;

public record ReleaseDownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double BytesPerSecond,
    TimeSpan? EstimatedRemaining);
