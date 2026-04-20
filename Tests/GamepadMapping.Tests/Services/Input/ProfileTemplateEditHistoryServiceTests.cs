#nullable enable
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using Xunit;

namespace GamepadMapping.Tests.Services.Input;

public class ProfileTemplateEditHistoryServiceTests
{
    [Fact]
    public void Undo_Restores_Previous_State_And_Enables_Redo()
    {
        GameProfileTemplate? live = new GameProfileTemplate { ProfileId = "a", DisplayName = "v0" };

        void Apply(GameProfileTemplate t) => live = t;

        var history = new ProfileTemplateEditHistoryService(
            () => live,
            Apply,
            () => true,
            maxUndoEntries: 50);

        live = new GameProfileTemplate { ProfileId = "a", DisplayName = "v1" };
        history.RecordCheckpoint();
        live = new GameProfileTemplate { ProfileId = "a", DisplayName = "v2" };

        Assert.True(history.CanUndo);
        history.Undo();

        Assert.Equal("v1", live!.DisplayName);
        Assert.True(history.CanRedo);
        Assert.False(history.CanUndo);

        history.Redo();
        Assert.Equal("v2", live!.DisplayName);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clear_Empties_Stacks()
    {
        var live = new GameProfileTemplate { ProfileId = "x", DisplayName = "a" };
        var history = new ProfileTemplateEditHistoryService(
            () => live,
            t => live = t,
            () => true);

        history.RecordCheckpoint();
        live = new GameProfileTemplate { ProfileId = "x", DisplayName = "b" };

        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void RecordCheckpoint_Raises_HistoryChanged()
    {
        var live = new GameProfileTemplate { ProfileId = "x", DisplayName = "a" };
        var history = new ProfileTemplateEditHistoryService(
            () => live,
            t => live = t,
            () => true);

        var raised = 0;
        history.HistoryChanged += (_, _) => raised++;

        history.RecordCheckpoint();
        Assert.Equal(1, raised);
    }
}
