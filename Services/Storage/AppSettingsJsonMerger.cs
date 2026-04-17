using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GamepadMapperGUI.Services.Storage;

/// <summary>
/// Merges shipped <c>default_settings.json</c> with the user's <c>local_settings.json</c> so new keys
/// from an updated default file are picked up while the updater preserves the local file. The overlay wins on conflicts.
/// Empty string values in the local file are treated as unset so defaults still apply (e.g. placeholder <c>""</c> after an update).
/// <see cref="SettingsService"/> persists the merged snapshot back to local when it differs from the file on disk.
/// </summary>
internal static class AppSettingsJsonMerger
{
    private static readonly JsonMergeSettings MergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
        MergeNullValueHandling = MergeNullValueHandling.Ignore
    };

    /// <summary>
    /// Returns a JSON object string suitable for deserializing to <see cref="Models.AppSettings"/>.
    /// </summary>
    /// <param name="defaultSettingsJson">Contents of default_settings.json, or null if missing/unreadable.</param>
    /// <param name="localSettingsJson">Contents of local_settings.json, or null if missing.</param>
    public static string MergeToJsonString(string? defaultSettingsJson, string? localSettingsJson)
    {
        var baseline = TryParseObject(defaultSettingsJson);
        var overlay = TryParseObject(localSettingsJson);

        if (baseline is null && overlay is null)
            return "{}";

        if (baseline is null)
            return StripEmptyStringLeaves((JObject)overlay!.DeepClone()).ToString(Formatting.None);

        var merged = (JObject)baseline.DeepClone();
        if (overlay is not null)
        {
            var overlayForMerge = StripEmptyStringLeaves((JObject)overlay.DeepClone());
            merged.Merge(overlayForMerge, MergeSettings);
        }

        return merged.ToString(Formatting.None);
    }

    /// <summary>
    /// Removes properties whose JSON value is the empty string, recursively under objects. Arrays are not modified.
    /// Used so local <c>""</c> does not override non-empty shipped defaults.
    /// </summary>
    internal static JObject StripEmptyStringLeaves(JObject root)
    {
        RemoveEmptyStringPropertiesRecursive(root);
        return root;
    }

    private static void RemoveEmptyStringPropertiesRecursive(JObject node)
    {
        var removeNames = new List<string>();
        foreach (var prop in node.Properties())
        {
            switch (prop.Value)
            {
                case JObject child:
                    RemoveEmptyStringPropertiesRecursive(child);
                    break;
                case JValue jv when jv.Type == JTokenType.String && jv.Value<string>() == "":
                    removeNames.Add(prop.Name);
                    break;
            }
        }

        foreach (var name in removeNames)
            node.Remove(name);
    }

    /// <summary>
    /// Returns null when <paramref name="json"/> is null/whitespace or not a JSON object.
    /// </summary>
    internal static JObject? TryParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var token = JToken.Parse(json);
            return token as JObject;
        }
        catch (JsonReaderException)
        {
            return null;
        }
    }
}
