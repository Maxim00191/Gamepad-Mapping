namespace GamepadMapperGUI.Models.State;

public record struct UpdateFailureToastPending(string? ErrorMessage, long TimestampUnixSeconds);
