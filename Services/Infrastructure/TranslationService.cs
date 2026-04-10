using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class TranslationService : INotifyPropertyChanged
{
    private static readonly ResourceManager ResourceManager =
        new("Gamepad_Mapping.Resources.Strings", Assembly.GetExecutingAssembly());

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (Equals(_culture, value))
                return;

            _culture = value;
            OnPropertyChanged(nameof(Culture));
            OnPropertyChanged("Item[]");
        }
    }

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var value = ResourceManager.GetString(key, _culture);
            return string.IsNullOrEmpty(value) ? $"[{key}]" : value;
        }
    }

    public void SetCulture(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return;

        Culture = CultureInfo.GetCultureInfo(cultureName);
    }

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

