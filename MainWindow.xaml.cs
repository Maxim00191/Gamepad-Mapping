using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using GamepadMapperGUI.Core;

namespace Gamepad_Mapping;

public partial class MainWindow : Window
{
    private GamepadReader _gamepadReader;
    public MainWindow()
    {
        InitializeComponent();
        _gamepadReader = new GamepadReader();

        _gamepadReader.OnButtonPressed += (buttons) =>
        {
            Debug.WriteLine($"Button pressed: {buttons}");
        };
        _gamepadReader.OnButtonReleased += (buttons) =>
        {
            Debug.WriteLine($"Button released: {buttons}");
        };

        _gamepadReader.OnLeftThumbstickChanged += (v) =>
        {
            Debug.WriteLine($"Left stick: {v.X:0.00}, {v.Y:0.00}");
        };
        _gamepadReader.OnRightThumbstickChanged += (v) =>
        {
            Debug.WriteLine($"Right stick: {v.X:0.00}, {v.Y:0.00}");
        };
        _gamepadReader.OnLeftTriggerChanged += (v) =>
        {
            Debug.WriteLine($"Left trigger: {v:0.00}");
        };
        _gamepadReader.OnRightTriggerChanged += (v) =>
        {
            Debug.WriteLine($"Right trigger: {v:0.00}");
        };

        _gamepadReader.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _gamepadReader.Stop();
        base.OnClosed(e);
    }
}