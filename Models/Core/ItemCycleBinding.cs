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
/// Cycles a shared hotbar index 1..n. Either taps digits (<c>D1</c>–<c>D9</c>) or, when both
/// <see cref="LoopForwardKey"/> and <see cref="LoopBackwardKey"/> are set, taps those outputs for next/previous
/// (same tokens as normal keyboard/mouse mapping). Optional <see cref="WithKeys"/> for keyboard chords.
/// </summary>
public sealed class ItemCycleBinding
{
    [JsonProperty("direction", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ItemCycleDirection Direction { get; set; } = ItemCycleDirection.Next;

    /// <summary>Number of slots (1–9). Wrap range for the shared index; with digit mode also selects <c>D1</c>..<c>Dn</c>.</summary>
    [JsonProperty("slotCount", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int SlotCount { get; set; } = 9;

    /// <summary>Output token for <see cref="ItemCycleDirection.Next"/> when using custom loop keys (must pair with <see cref="LoopBackwardKey"/>).</summary>
    [JsonProperty("loopForwardKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? LoopForwardKey { get; set; }

    /// <summary>Output token for <see cref="ItemCycleDirection.Previous"/> when using custom loop keys (must pair with <see cref="LoopForwardKey"/>).</summary>
    [JsonProperty("loopBackwardKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? LoopBackwardKey { get; set; }

    /// <summary>Keys held while tapping the digit (e.g. <c>LeftAlt</c>, <c>LeftCtrl</c>). Press order matches list order.</summary>
    [JsonProperty("withKeys", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? WithKeys { get; set; }
}
