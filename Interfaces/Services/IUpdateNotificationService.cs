using GamepadMapperGUI.Models.State;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IUpdateNotificationService
{
    UpdateSuccessToastPending? TryGetPendingSuccessToast();

    UpdateFailureToastPending? TryGetPendingFailureToast();

    void AcknowledgeSuccess(long updatedAtUnixSeconds);

    void AcknowledgeFailure();
}
