using System;
using System.ComponentModel;
using System.Windows.Input;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IKeyboardCaptureService : INotifyPropertyChanged
{
    bool IsRecordingKeyboardKey { get; }
    string KeyboardKeyCapturePrompt { get; }
    void BeginCapture(string prompt, Action<Key> onCaptured);
    bool TryCaptureKeyboardKey(Key key, Key? systemKey = null);
    void CancelCapture();
}
