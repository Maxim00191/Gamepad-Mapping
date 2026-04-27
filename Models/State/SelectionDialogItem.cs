namespace Gamepad_Mapping.Models.State;

public sealed record SelectionDialogItem(
    string Key,
    string PrimaryText,
    string SecondaryText,
    string SearchText);
