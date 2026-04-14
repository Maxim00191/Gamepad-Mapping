using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Services.ControllerVisual;

public static class ControllerVisualManifestParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static bool TryParse(string json, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ControllerVisualLayoutDescriptor? descriptor)
    {
        descriptor = null;
        try
        {
            var dto = JsonSerializer.Deserialize<ManifestDto>(json, Options);
            if (dto is null) return false;
            if (string.IsNullOrWhiteSpace(dto.LayoutKey)) return false;
            if (string.IsNullOrWhiteSpace(dto.SvgFile)) return false;
            if (dto.Regions is null || dto.Regions.Count == 0) return false;

            var regions = new List<ControllerVisualRegionDefinition>(dto.Regions.Count);
            foreach (var r in dto.Regions)
            {
                if (string.IsNullOrWhiteSpace(r.LogicalId) || string.IsNullOrWhiteSpace(r.SvgElementId))
                {
                    Debug.WriteLine("Controller layout manifest: skipped region with empty logicalId or svgElementId.");
                    continue;
                }

                regions.Add(new ControllerVisualRegionDefinition(
                    r.LogicalId.Trim(),
                    r.SvgElementId.Trim(),
                    ParseKind(r.ElementKind)));
            }

            if (regions.Count == 0) return false;

            descriptor = new ControllerVisualLayoutDescriptor(
                dto.LayoutKey.Trim(),
                dto.SvgFile.Trim(),
                regions,
                string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim());
            return true;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Controller layout manifest JSON error: {ex.Message}");
            return false;
        }
    }

    private static ControllerVisualElementKind ParseKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ControllerVisualElementKind.Auto;
        if (string.Equals(raw, "path", StringComparison.OrdinalIgnoreCase)) return ControllerVisualElementKind.Path;
        if (string.Equals(raw, "rect", StringComparison.OrdinalIgnoreCase)) return ControllerVisualElementKind.Rect;
        return ControllerVisualElementKind.Auto;
    }

    private sealed class ManifestDto
    {
        [JsonPropertyName("layoutKey")]
        public string? LayoutKey { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("svgFile")]
        public string? SvgFile { get; set; }

        [JsonPropertyName("regions")]
        public List<RegionDto>? Regions { get; set; }
    }

    private sealed class RegionDto
    {
        [JsonPropertyName("logicalId")]
        public string? LogicalId { get; set; }

        [JsonPropertyName("svgElementId")]
        public string? SvgElementId { get; set; }

        [JsonPropertyName("elementKind")]
        public string? ElementKind { get; set; }
    }
}
