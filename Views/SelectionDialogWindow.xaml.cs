using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views;

public partial class SelectionDialogWindow
{
    public SelectionDialogWindow()
    {
        InitializeComponent();
    }

    private void Select_Click(object sender, RoutedEventArgs e) => CommitAndClose();

    private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            CommitAndClose();
    }

    private void CommitAndClose()
    {
        if (DataContext is not SelectionDialogViewModel vm || vm.SelectedItem is null)
            return;

        DialogResult = true;
        Close();
    }
}
