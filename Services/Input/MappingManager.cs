using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Input;

/// <summary>
/// Manages the mapping state and synchronizes it with the mapping engine.
/// </summary>
public partial class MappingManager : ObservableObject, IMappingManager
{
    private static readonly TimeSpan OutputQueueDrainBeforeEngineDispose = TimeSpan.FromSeconds(2);

    private IMappingEngine _engine;
    private readonly IProfileService _profileService;
    private IReadOnlyList<MappingEntry> _mappingsSnapshot = Array.Empty<MappingEntry>();

    public MappingManager(IMappingEngine engine, IProfileService profileService)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));

        Mappings = new ObservableCollection<MappingEntry>();
        KeyboardActions = new ObservableCollection<KeyboardActionDefinition>();
        RadialMenus = new ObservableCollection<RadialMenuDefinition>();

        Mappings.CollectionChanged += (_, _) =>
        {
            _mappingsSnapshot = Mappings.ToList();
            MappingsChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public ObservableCollection<MappingEntry> Mappings { get; }
    public ObservableCollection<KeyboardActionDefinition> KeyboardActions { get; }
    public ObservableCollection<RadialMenuDefinition> RadialMenus { get; }

    [ObservableProperty]
    private MappingEntry? selectedMapping;

    public int MappingCount => Mappings.Count;

    public event EventHandler? MappingsChanged;
    public event Action<InputFrame, InputFrameProcessingResult>? OnInputProcessed;

    public void LoadTemplate(GameProfileTemplate template)
    {
        if (template is null) return;

        SelectedMapping = null;
        KeyboardActions.Clear();
        RadialMenus.Clear();
        foreach (var a in template.KeyboardActions ?? []) KeyboardActions.Add(a);
        foreach (var rm in template.RadialMenus ?? []) RadialMenus.Add(rm);

        _engine.SetComboLeadButtonsFromTemplate(template.ComboLeadButtons);
        RefreshEngineDefinitions();

        Mappings.Clear();
        foreach (var mapping in template.Mappings) Mappings.Add(mapping);
        _mappingsSnapshot = Mappings.ToList();
    }

    public void ProcessInputFrame(InputFrame frame, bool allowOutput)
    {
        var result = _engine.ProcessInputFrame(frame, _mappingsSnapshot, allowOutput);
        OnInputProcessed?.Invoke(frame, result);
    }

    public void RefreshEngineDefinitions()
    {
        _engine.SetRadialMenuDefinitions(
            RadialMenus.Count == 0 ? null : RadialMenus.ToList(),
            KeyboardActions.Count == 0 ? null : KeyboardActions.ToList());
    }

    public void ForceReleaseOutputs()
    {
        _engine.ForceReleaseAllOutputs();
        _engine.ForceReleaseAnalogOutputs();
    }

    public void ReplaceEngine(IMappingEngine newEngine, IReadOnlyList<string>? comboLeadButtons)
    {
        ArgumentNullException.ThrowIfNull(newEngine);
        ForceReleaseOutputs();
        TryWaitForEngineOutputQueueIdle(_engine);
        _engine.Dispose();
        _engine = newEngine;
        _engine.SetComboLeadButtonsFromTemplate(comboLeadButtons);
        foreach (var m in Mappings)
            m.ExecutableAction = null;
        RefreshEngineDefinitions();
    }

    private static void TryWaitForEngineOutputQueueIdle(IMappingEngine engine)
    {
        try
        {
            engine.WaitForIdleAsync().Wait(OutputQueueDrainBeforeEngineDispose);
        }
        catch (Exception)
        {
            // Best-effort: teardown must not hang the UI if a worker is stuck.
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}


