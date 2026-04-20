#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamepad_Mapping.ViewModels;

/// <summary>Copy/paste and undo/redo chrome for the profile template workspace (mappings, keyboard actions, radial menus).</summary>
public partial class ProfileRuleClipboardViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IProfileRuleClipboardService _clipboard;
    private readonly IAppToastService _toast;
    private readonly IProfileTemplateEditHistoryService _editHistory;

    public ProfileRuleClipboardViewModel(
        MainViewModel main,
        IProfileRuleClipboardService clipboard,
        IAppToastService toast,
        IProfileTemplateEditHistoryService editHistory)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        _editHistory = editHistory ?? throw new ArgumentNullException(nameof(editHistory));
    }

    public void RefreshCommandStates()
    {
        CopyRuleCommand.NotifyCanExecuteChanged();
        PasteRuleCommand.NotifyCanExecuteChanged();
        SelectAllWorkspaceRulesCommand.NotifyCanExecuteChanged();
        UndoWorkspaceEditCommand.NotifyCanExecuteChanged();
        RedoWorkspaceEditCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndoWorkspaceEdit() =>
        _main.SelectedTemplate is not null && _editHistory.CanUndo;

    private bool CanRedoWorkspaceEdit() =>
        _main.SelectedTemplate is not null && _editHistory.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoWorkspaceEdit))]
    private void UndoWorkspaceEdit() => _editHistory.Undo();

    [RelayCommand(CanExecute = nameof(CanRedoWorkspaceEdit))]
    private void RedoWorkspaceEdit() => _editHistory.Redo();

    private bool CanCopy() =>
        _main.SelectedTemplate is not null
        && _main.ProfileListTabIndex switch
        {
            (int)MainViewModel.MainProfileWorkspaceTab.Mappings =>
                !_main.MappingEditorPanel.IsCreatingNewMapping
                && (_main.MappingEditorPanel.WorkspaceSelectedMappings.Count > 0 || _main.SelectedMapping is not null),
            (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions =>
                _main.CatalogPanel.WorkspaceSelectedKeyboardActions.Count > 0
                || _main.CatalogPanel.SelectedKeyboardAction is not null,
            (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus =>
                _main.CatalogPanel.WorkspaceSelectedRadialMenus.Count > 0
                || _main.CatalogPanel.SelectedRadialMenu is not null,
            _ => false
        };

    private bool CanSelectAllWorkspaceRules() =>
        _main.SelectedTemplate is not null
        && _main.ProfileListTabIndex switch
        {
            (int)MainViewModel.MainProfileWorkspaceTab.Mappings =>
                !_main.MappingEditorPanel.IsCreatingNewMapping && _main.Mappings.Count > 0,
            (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions => _main.KeyboardActions.Count > 0,
            (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus => _main.RadialMenus.Count > 0,
            _ => false
        };

    private bool CanPaste()
    {
        if (_main.SelectedTemplate is null)
            return false;
        if (!_clipboard.TryGet(out var env) || env is null)
            return false;

        return _main.ProfileListTabIndex switch
        {
            (int)MainViewModel.MainProfileWorkspaceTab.Mappings => env.Kind == ProfileRuleClipboardKind.Mapping,
            (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions => env.Kind == ProfileRuleClipboardKind.KeyboardAction,
            (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus => env.Kind == ProfileRuleClipboardKind.RadialMenu,
            _ => false
        };
    }

    [RelayCommand(CanExecute = nameof(CanSelectAllWorkspaceRules))]
    private void SelectAllWorkspaceRules()
    {
        switch (_main.ProfileListTabIndex)
        {
            case (int)MainViewModel.MainProfileWorkspaceTab.Mappings:
                _main.MappingEditorPanel.SelectAllMappingsForWorkspace();
                break;
            case (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions:
                _main.CatalogPanel.SelectAllKeyboardActionsForWorkspace();
                break;
            case (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus:
                _main.CatalogPanel.SelectAllRadialMenusForWorkspace();
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void CopyRule()
    {
        try
        {
            string? payloadJson = null;
            ProfileRuleClipboardKind? kind = null;

            switch (_main.ProfileListTabIndex)
            {
                case (int)MainViewModel.MainProfileWorkspaceTab.Mappings:
                    if (_main.MappingEditorPanel.IsCreatingNewMapping)
                        return;
                    var mapItems = _main.MappingEditorPanel.WorkspaceSelectedMappings.Count > 0
                        ? _main.MappingEditorPanel.WorkspaceSelectedMappings.ToList()
                        : _main.SelectedMapping is { } sm
                            ? new List<MappingEntry> { sm }
                            : [];
                    if (mapItems.Count == 0)
                        return;
                    kind = ProfileRuleClipboardKind.Mapping;
                    payloadJson = mapItems.Count == 1
                        ? JsonConvert.SerializeObject(mapItems[0])
                        : JsonConvert.SerializeObject(mapItems);
                    break;
                case (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions:
                    var kbdItems = _main.CatalogPanel.WorkspaceSelectedKeyboardActions.Count > 0
                        ? _main.CatalogPanel.WorkspaceSelectedKeyboardActions.ToList()
                        : _main.CatalogPanel.SelectedKeyboardAction is { } sk
                            ? new List<KeyboardActionDefinition> { sk }
                            : [];
                    if (kbdItems.Count == 0)
                        return;
                    kind = ProfileRuleClipboardKind.KeyboardAction;
                    payloadJson = kbdItems.Count == 1
                        ? JsonConvert.SerializeObject(kbdItems[0])
                        : JsonConvert.SerializeObject(kbdItems);
                    break;
                case (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus:
                    var radItems = _main.CatalogPanel.WorkspaceSelectedRadialMenus.Count > 0
                        ? _main.CatalogPanel.WorkspaceSelectedRadialMenus.ToList()
                        : _main.CatalogPanel.SelectedRadialMenu is { } sr
                            ? new List<RadialMenuDefinition> { sr }
                            : [];
                    if (radItems.Count == 0)
                        return;
                    kind = ProfileRuleClipboardKind.RadialMenu;
                    payloadJson = radItems.Count == 1
                        ? JsonConvert.SerializeObject(radItems[0])
                        : JsonConvert.SerializeObject(radItems);
                    break;
                default:
                    return;
            }

            if (kind is null || payloadJson is null)
                return;

            _clipboard.Store(new ProfileRuleClipboardEnvelope
            {
                Kind = kind.Value,
                PayloadJson = payloadJson
            });
        }
        catch (Exception ex)
        {
            ShowClipboardToast("ProfileRuleClipboard_CopyFailedTitle", "ProfileRuleClipboard_CopyFailedMessage", ex.Message);
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void PasteRule()
    {
        if (!_clipboard.TryGet(out var env) || env is null)
            return;

        _main.RecordTemplateWorkspaceCheckpoint();

        try
        {
            switch (env.Kind)
            {
                case ProfileRuleClipboardKind.Mapping:
                    PasteMapping(env.PayloadJson);
                    break;
                case ProfileRuleClipboardKind.KeyboardAction:
                    PasteKeyboardAction(env.PayloadJson);
                    break;
                case ProfileRuleClipboardKind.RadialMenu:
                    PasteRadialMenu(env.PayloadJson);
                    break;
                default:
                    ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_UnsupportedPayload");
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PasteFailedMessage", ex.Message);
        }

        RefreshCommandStates();
    }

    private void PasteMapping(string payloadJson)
    {
        JToken token;
        try
        {
            token = JToken.Parse(payloadJson);
        }
        catch (JsonException)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
            return;
        }

        if (token is JArray arr)
        {
            MappingEntry? last = null;
            foreach (var item in arr)
            {
                var clone = item.ToObject<MappingEntry>();
                if (clone is null)
                    continue;
                clone.ExecutableAction = null;
                _main.Mappings.Add(clone);
                last = clone;
            }

            if (last is null)
            {
                ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
                return;
            }

            _main.SelectedMapping = last;
            _main.RefreshAfterRulePastedFromClipboard();
            return;
        }

        var single = token.ToObject<MappingEntry>();
        if (single is null)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
            return;
        }

        single.ExecutableAction = null;
        _main.Mappings.Add(single);
        _main.SelectedMapping = single;
        _main.RefreshAfterRulePastedFromClipboard();
    }

    private void PasteKeyboardAction(string payloadJson)
    {
        JToken token;
        try
        {
            token = JToken.Parse(payloadJson);
        }
        catch (JsonException)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
            return;
        }

        if (token is JArray arr)
        {
            KeyboardActionDefinition? last = null;
            foreach (var item in arr)
            {
                var clone = item.ToObject<KeyboardActionDefinition>();
                if (clone is null)
                    continue;
                clone.Id = EnsureUniqueKeyboardActionId(clone.Id, _main.KeyboardActions);
                _main.KeyboardActions.Add(clone);
                last = clone;
            }

            if (last is null)
            {
                ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
                return;
            }

            _main.CatalogPanel.SelectedKeyboardAction = last;
            _main.RefreshMappingEngineDefinitions();
            return;
        }

        var single = token.ToObject<KeyboardActionDefinition>();
        if (single is null)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
            return;
        }

        single.Id = EnsureUniqueKeyboardActionId(single.Id, _main.KeyboardActions);
        _main.KeyboardActions.Add(single);
        _main.CatalogPanel.SelectedKeyboardAction = single;
        _main.RefreshMappingEngineDefinitions();
    }

    private void PasteRadialMenu(string payloadJson)
    {
        JToken token;
        try
        {
            token = JToken.Parse(payloadJson);
        }
        catch (JsonException)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
            return;
        }

        if (token is JArray arr)
        {
            RadialMenuDefinition? last = null;
            foreach (var item in arr)
            {
                var clone = item.ToObject<RadialMenuDefinition>();
                if (clone is null)
                    continue;
                clone.Id = EnsureUniqueRadialMenuId(clone.Id, _main.RadialMenus);
                NormalizeRadialItems(clone);
                _main.RadialMenus.Add(clone);
                last = clone;
            }

            if (last is null)
            {
                ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
                return;
            }

            _main.CatalogPanel.SelectedRadialMenu = last;
            _main.RefreshMappingEngineDefinitions();
            return;
        }

        var single = token.ToObject<RadialMenuDefinition>();
        if (single is null)
        {
            ShowClipboardToast("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PayloadInvalid");
            return;
        }

        single.Id = EnsureUniqueRadialMenuId(single.Id, _main.RadialMenus);
        NormalizeRadialItems(single);
        _main.RadialMenus.Add(single);
        _main.CatalogPanel.SelectedRadialMenu = single;
        _main.RefreshMappingEngineDefinitions();
    }

    private static void NormalizeRadialItems(RadialMenuDefinition menu)
    {
        if (menu.Items is null)
        {
            menu.Items = new ObservableCollection<RadialMenuItem>();
            return;
        }

        if (menu.Items is not ObservableCollection<RadialMenuItem>)
            menu.Items = new ObservableCollection<RadialMenuItem>(menu.Items);
    }

    private static string EnsureUniqueKeyboardActionId(string? preferred, ObservableCollection<KeyboardActionDefinition> actions)
    {
        var p = (preferred ?? string.Empty).Trim();
        if (p.Length == 0)
            p = "action";

        if (!ContainsId(actions, p))
            return p;

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{p}_{i}";
            if (!ContainsId(actions, candidate))
                return candidate;
        }

        return $"{p}_{Guid.NewGuid():N}"[..12];
    }

    private static bool ContainsId(ObservableCollection<KeyboardActionDefinition> actions, string id) =>
        actions.Any(a => string.Equals((a.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));

    private static string EnsureUniqueRadialMenuId(string? preferred, ObservableCollection<RadialMenuDefinition> menus)
    {
        var p = (preferred ?? string.Empty).Trim();
        if (p.Length == 0)
            p = "radial";

        if (!ContainsRadialId(menus, p))
            return p;

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{p}_{i}";
            if (!ContainsRadialId(menus, candidate))
                return candidate;
        }

        return $"{p}_{Guid.NewGuid():N}"[..12];
    }

    private static bool ContainsRadialId(ObservableCollection<RadialMenuDefinition> menus, string id) =>
        menus.Any(r => string.Equals((r.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));

    private void ShowClipboardToast(string titleKey, string messageKey, params object[] formatArgs)
    {
        var title = Loc(titleKey);
        var template = Loc(messageKey);
        var message = formatArgs.Length > 0
            ? string.Format(System.Globalization.CultureInfo.CurrentUICulture, template, formatArgs)
            : template;
        _toast.Show(new AppToastRequest
        {
            Title = title,
            Message = message,
            AutoHideSeconds = 8
        });
    }

    private static string Loc(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];
        return key;
    }
}
