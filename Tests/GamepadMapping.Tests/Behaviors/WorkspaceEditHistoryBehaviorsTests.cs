using System.Threading;
using System.Windows.Controls;
using Gamepad_Mapping.Behaviors;

namespace GamepadMapping.Tests.Behaviors;

public class WorkspaceEditHistoryBehaviorsTests
{
    [Fact]
    public void DataGrid_RecordCheckpointOnBeginningEdit_Default_IsFalse() =>
        RunSta(() =>
        {
            var grid = new DataGrid();
            Assert.False(DataGridWorkspaceEditHistoryBehavior.GetRecordCheckpointOnBeginningEdit(grid));
        });

    [Fact]
    public void DataGrid_RecordCheckpointOnBeginningEdit_CanBeToggled() =>
        RunSta(() =>
        {
            var grid = new DataGrid();
            DataGridWorkspaceEditHistoryBehavior.SetRecordCheckpointOnBeginningEdit(grid, true);
            Assert.True(DataGridWorkspaceEditHistoryBehavior.GetRecordCheckpointOnBeginningEdit(grid));
            DataGridWorkspaceEditHistoryBehavior.SetRecordCheckpointOnBeginningEdit(grid, false);
            Assert.False(DataGridWorkspaceEditHistoryBehavior.GetRecordCheckpointOnBeginningEdit(grid));
        });

    [Fact]
    public void FrameworkElement_RecordCheckpointWhenFocusEntersFromOutside_Default_IsFalse() =>
        RunSta(() =>
        {
            var el = new UserControl();
            Assert.False(FrameworkElementWorkspaceEditHistoryBehavior.GetRecordCheckpointWhenFocusEntersFromOutside(el));
        });

    [Fact]
    public void FrameworkElement_RecordCheckpointWhenFocusEntersFromOutside_CanBeToggled() =>
        RunSta(() =>
        {
            var el = new UserControl();
            FrameworkElementWorkspaceEditHistoryBehavior.SetRecordCheckpointWhenFocusEntersFromOutside(el, true);
            Assert.True(FrameworkElementWorkspaceEditHistoryBehavior.GetRecordCheckpointWhenFocusEntersFromOutside(el));
        });

    private static void RunSta(Action action)
    {
        Exception? caught = null;
        var t = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (caught is not null)
            throw caught;
    }
}
