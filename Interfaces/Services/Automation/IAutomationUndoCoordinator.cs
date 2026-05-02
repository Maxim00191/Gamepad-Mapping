namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationUndoCoordinator
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    void PushCheckpoint(string serializedDocumentBeforeMutation);

    bool TryUndo(ref string serializedDocumentApply);

    bool TryRedo(ref string serializedDocumentApply);

    void Clear();
}
