using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core.Input;

namespace GamepadMapperGUI.Services.Input;

public sealed class GamepadSourceFactory : IGamepadSourceFactory
{
    private delegate IGamepadSource SourceFactory(IXInput xInput, IPlayStationInputProvider playStationInputProvider);

    private sealed record SourceDescriptor(
        string Id,
        string DisplayNameLocalizationKey,
        bool IsImplemented,
        SourceFactory Factory);

    private static readonly IReadOnlyList<SourceDescriptor> SourceDescriptors =
    [
        new(
            GamepadSourceApiIds.XInput,
            "GamepadSourceXInputLabel",
            true,
            static (x, _) => new XInputSource(x)),
        new(
            GamepadSourceApiIds.PlayStation,
            "GamepadSourcePlayStationLabel",
            true,
            static (_, ps) => new PlayStationNativeSource(ps))
    ];

    private static readonly IReadOnlyDictionary<string, SourceDescriptor> DescriptorById =
        SourceDescriptors.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<GamepadSourceRegistration> Registrations =
        SourceDescriptors
            .Select(static descriptor => new GamepadSourceRegistration(
                descriptor.Id,
                descriptor.DisplayNameLocalizationKey,
                descriptor.IsImplemented))
            .ToArray();

    private static readonly IReadOnlyDictionary<string, string> LegacyApiAliasToCanonical =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GamepadSourceApiIds.DualSense] = GamepadSourceApiIds.PlayStation
        };

    private readonly IXInput _xInput;
    private readonly IPlayStationInputProvider _playStationInputProvider;

    public GamepadSourceFactory(IXInput xInput, IPlayStationInputProvider? playStationInputProvider = null)
    {
        _xInput = xInput ?? throw new ArgumentNullException(nameof(xInput));
        _playStationInputProvider = playStationInputProvider ?? new DualSenseHidInputProvider();
    }

    public IReadOnlyList<GamepadSourceRegistration> GetRegistrations() => Registrations;

    public string NormalizeApiId(string? apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
            return GamepadSourceApiIds.XInput;

        var trimmed = apiId.Trim();
        if (LegacyApiAliasToCanonical.TryGetValue(trimmed, out var aliasedApiId))
            trimmed = aliasedApiId;

        if (DescriptorById.TryGetValue(trimmed, out var descriptor))
            return descriptor.Id;

        return GamepadSourceApiIds.XInput;
    }

    public IGamepadSource CreateSource(string? requestedApiId, out string resolvedApiId)
    {
        resolvedApiId = NormalizeApiId(requestedApiId);
        if (!DescriptorById.TryGetValue(resolvedApiId, out var descriptor))
            descriptor = DescriptorById[GamepadSourceApiIds.XInput];

        return descriptor.Factory(_xInput, _playStationInputProvider);
    }
}
