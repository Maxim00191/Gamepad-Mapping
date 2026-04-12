using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;

namespace GamepadMapperGUI.Services.Input;

public partial class KeyboardCaptureService : ObservableObject, IKeyboardCaptureService
{
    private Action<Key>? _captureHandler;

    [ObservableProperty]
    private bool isRecordingKeyboardKey;

    [ObservableProperty]
    private string keyboardKeyCapturePrompt = string.Empty;

    public void BeginCapture(string prompt, Action<Key> onCaptured)
    {
        if (onCaptured is null)
            return;

        _captureHandler = onCaptured;
        KeyboardKeyCapturePrompt = prompt ?? string.Empty;
        IsRecordingKeyboardKey = true;
    }

    public bool TryCaptureKeyboardKey(Key key, Key? systemKey = null)
    {
        if (!IsRecordingKeyboardKey || _captureHandler is null)
            return false;

        var recordedKey = key == Key.System && systemKey.HasValue ? systemKey.Value : key;
        if (recordedKey == Key.None || recordedKey == Key.System)
            return false;

        _captureHandler(recordedKey);
        CancelCapture();
        return true;
    }

    public void CancelCapture()
    {
        _captureHandler = null;
        IsRecordingKeyboardKey = false;
        KeyboardKeyCapturePrompt = string.Empty;
    }
}


