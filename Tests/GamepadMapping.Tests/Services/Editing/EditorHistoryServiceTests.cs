#nullable enable
using GamepadMapperGUI.Services.Editing;
using Xunit;

namespace GamepadMapping.Tests.Services.Editing;

public class EditorHistoryServiceTests
{
    [Fact]
    public void Undo_Restores_Previous_State_And_Enables_Redo()
    {
        EditorHistoryTestSnapshot live = new() { Label = "v0" };

        var history = new EditorHistoryService<EditorHistoryTestSnapshot>(
            () => new EditorHistoryTestSnapshot { Label = live.Label },
            snap => live.Label = snap.Label,
            () => true,
            maxUndoEntries: 50);

        live.Label = "v1";
        history.RecordCheckpoint();
        live.Label = "v2";

        Assert.True(history.CanUndo);
        history.Undo();

        Assert.Equal("v1", live.Label);
        Assert.True(history.CanRedo);
        Assert.False(history.CanUndo);

        history.Redo();
        Assert.Equal("v2", live.Label);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clear_Empties_Stacks()
    {
        EditorHistoryTestSnapshot live = new() { Label = "a" };
        var history = new EditorHistoryService<EditorHistoryTestSnapshot>(
            () => new EditorHistoryTestSnapshot { Label = live.Label },
            snap => live.Label = snap.Label,
            () => true);

        history.RecordCheckpoint();
        live.Label = "b";

        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void RecordCheckpoint_Raises_HistoryChanged()
    {
        EditorHistoryTestSnapshot live = new() { Label = "a" };
        var history = new EditorHistoryService<EditorHistoryTestSnapshot>(
            () => new EditorHistoryTestSnapshot { Label = live.Label },
            snap => live.Label = snap.Label,
            () => true);

        var raised = 0;
        history.HistoryChanged += (_, _) => raised++;

        history.RecordCheckpoint();
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ExecuteTransaction_WhenStateDoesNotChange_DoesNotCreateUndoEntry()
    {
        EditorHistoryTestSnapshot live = new() { Label = "a" };
        var history = new EditorHistoryService<EditorHistoryTestSnapshot>(
            () => new EditorHistoryTestSnapshot { Label = live.Label },
            snap => live.Label = snap.Label,
            () => true);

        history.ExecuteTransaction(() =>
        {
            // Intentionally no-op.
        });

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ExecuteTransaction_WhenStateChanges_CreatesUndoEntry()
    {
        EditorHistoryTestSnapshot live = new() { Label = "a" };
        var history = new EditorHistoryService<EditorHistoryTestSnapshot>(
            () => new EditorHistoryTestSnapshot { Label = live.Label },
            snap => live.Label = snap.Label,
            () => true);

        history.ExecuteTransaction(() =>
        {
            live.Label = "b";
        });

        Assert.True(history.CanUndo);
        history.Undo();
        Assert.Equal("a", live.Label);
    }

    [Fact]
    public void RecordCheckpoint_DoesNotDuplicateIdenticalSnapshot()
    {
        EditorHistoryTestSnapshot live = new() { Label = "a" };
        var history = new EditorHistoryService<EditorHistoryTestSnapshot>(
            () => new EditorHistoryTestSnapshot { Label = live.Label },
            snap => live.Label = snap.Label,
            () => true);

        history.RecordCheckpoint();
        history.RecordCheckpoint();

        history.Undo();

        Assert.False(history.CanUndo);
    }
}
