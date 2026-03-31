using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class AppSettings
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("templatesDirectory")]
    public string TemplatesDirectory { get; set; } = "Assets/Profiles/templates";

    [JsonProperty("defaultGameId")]
    public string DefaultGameId { get; set; } = "default";

    /// <summary>Profile id (<c>*.json</c> stem) of the template last chosen in the UI; restored on next launch.</summary>
    [JsonProperty("lastSelectedTemplateProfileId")]
    public string? LastSelectedTemplateProfileId { get; set; }

    /// <summary>
    /// Shared timing (ms) for chord modifier grace and combo HUD reveal delay. Hold-bind duration uses each mapping's
    /// <c>holdThresholdMs</c> when set; otherwise this value is the hold threshold fallback. Clamped when applied.
    /// </summary>
    [JsonProperty("modifierGraceMs")]
    public int ModifierGraceMs { get; set; } = 500;

    /// <summary>
    /// Applies only to buttons listed as combo leads in the profile (<c>comboLeadButtons</c>) or inferred from mappings.
    /// If held longer than this (ms) and then released without a combo path, suppress the solo Released mapping and hold-dual
    /// short (tap) so cancelling a combo does not fire those keys. Clamped when applied.
    /// </summary>
    [JsonProperty("leadKeyReleaseSuppressMs")]
    public int LeadKeyReleaseSuppressMs { get; set; } = 500;

    /// <summary>
    /// Legacy shared thumbstick deadzone (normalized [0..1]). When per-stick values are unset, this acts as the default.
    /// </summary>
    [JsonProperty("thumbstickDeadzone")]
    public float ThumbstickDeadzone { get; set; } = 0.10f;

    /// <summary>Normalized [0..1] deadzone for the left stick; falls back to <see cref="ThumbstickDeadzone"/> when non-positive.</summary>
    [JsonProperty("leftThumbstickDeadzone")]
    public float LeftThumbstickDeadzone { get; set; }

    /// <summary>Normalized [0..1] deadzone for the right stick; falls back to <see cref="ThumbstickDeadzone"/> when non-positive.</summary>
    [JsonProperty("rightThumbstickDeadzone")]
    public float RightThumbstickDeadzone { get; set; }

    /// <summary>Left trigger inner deadzone: raw values at or below this normalized level map to 0.</summary>
    [JsonProperty("leftTriggerInnerDeadzone")]
    public float LeftTriggerInnerDeadzone { get; set; }

    /// <summary>Left trigger outer threshold: raw values at or above this normalized level map to 1 (full pull).</summary>
    [JsonProperty("leftTriggerOuterDeadzone")]
    public float LeftTriggerOuterDeadzone { get; set; } = 1f;

    /// <summary>Right trigger inner deadzone (same semantics as <see cref="LeftTriggerInnerDeadzone"/>).</summary>
    [JsonProperty("rightTriggerInnerDeadzone")]
    public float RightTriggerInnerDeadzone { get; set; }

    /// <summary>Right trigger outer threshold (same semantics as <see cref="LeftTriggerOuterDeadzone"/>).</summary>
    [JsonProperty("rightTriggerOuterDeadzone")]
    public float RightTriggerOuterDeadzone { get; set; } = 1f;

    /// <summary>
    /// Polling interval for reading the gamepad state (ms). Lower values increase responsiveness at the cost of CPU usage.
    /// </summary>
    [JsonProperty("gamepadPollingIntervalMs")]
    public int GamepadPollingIntervalMs { get; set; } = 10;

    /// <summary>
    /// Default normalized [0..1] threshold for activating stick/trigger analog mappings when a mapping does not override it.
    /// </summary>
    [JsonProperty("defaultAnalogActivationThreshold")]
    public float DefaultAnalogActivationThreshold { get; set; } = 0.35f;

    /// <summary>
    /// Sensitivity factor for mouse-look mappings (pixels per unit of stick input) when a mapping does not override it.
    /// </summary>
    [JsonProperty("mouseLookSensitivity")]
    public float MouseLookSensitivity { get; set; } = 18f;

    /// <summary>
    /// Minimum analog change magnitude required before emitting a new input frame (epsilon). Smaller values are more sensitive.
    /// </summary>
    [JsonProperty("analogChangeEpsilon")]
    public float AnalogChangeEpsilon { get; set; } = 0.01f;

    /// <summary>
    /// Default keyboard tap hold duration in milliseconds when simulating a single key press.
    /// </summary>
    [JsonProperty("keyboardTapHoldDurationMs")]
    public int KeyboardTapHoldDurationMs { get; set; } = 30;

    /// <summary>
    /// Default delay in milliseconds between repeated tap outputs when a mapping requests multiple repeats.
    /// </summary>
    [JsonProperty("tapInterKeyDelayMs")]
    public int TapInterKeyDelayMs { get; set; } = 0;

    /// <summary>
    /// Default delay in milliseconds between characters when sending text through the keyboard emulator.
    /// </summary>
    [JsonProperty("textInterCharDelayMs")]
    public int TextInterCharDelayMs { get; set; } = 0;
}
