using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GamepadMapperGUI.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum ItemCycleDirection
{
    Next,
    Previous
}

/// <summary>
/// Cycles a shared hotbar index 1..n: each activation taps the corresponding number key
/// (<see cref="System.Windows.Input.Key.D1"/>–<see cref="System.Windows.Input.Key.D9"/>), optionally with
/// <see cref="WithKeys"/> held as modifiers (keyboard chord).
/// </summary>
public sealed class ItemCycleBinding
{
    [JsonProperty("direction", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ItemCycleDirection Direction { get; set; } = ItemCycleDirection.Next;

    /// <summary>Number of slots (1–9). Maps to digit keys 1 through <see cref="SlotCount"/>.</summary>
    [JsonProperty("slotCount", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int SlotCount { get; set; } = 9;

    /// <summary>Keys held while tapping the digit (e.g. <c>LeftAlt</c>, <c>LeftCtrl</c>). Press order matches list order.</summary>
    [JsonProperty("withKeys", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? WithKeys { get; set; }
}
