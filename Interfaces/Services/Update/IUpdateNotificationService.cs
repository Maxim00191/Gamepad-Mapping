using GamepadMapperGUI.Models.State;

namespace GamepadMapperGUI.Interfaces.Services.Update;

public interface IUpdateNotificationService
{
    UpdateSuccessToastPending? TryGetPendingSuccessToast();

    UpdateFailureToastPending? TryGetPendingFailureToast();

    void AcknowledgeSuccess(long updatedAtUnixSeconds);

    void AcknowledgeFailure();
}

