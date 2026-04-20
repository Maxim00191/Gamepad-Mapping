#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Forwards item-level <see cref="INotifyPropertyChanged"/> from workspace collections into a single callback (dirty-state refresh).
/// </summary>
public sealed class WorkspaceObservableMutationWatcher : IDisposable
{
    private readonly Action _onMutated;
    private readonly ObservableCollection<MappingEntry> _mappings;
    private readonly ObservableCollection<KeyboardActionDefinition> _keyboardActions;
    private readonly ObservableCollection<RadialMenuDefinition> _radialMenus;

    private readonly NotifyCollectionChangedEventHandler _mappingsHandler;
    private readonly NotifyCollectionChangedEventHandler _keyboardHandler;
    private readonly NotifyCollectionChangedEventHandler _radialHandler;
    private readonly PropertyChangedEventHandler _itemHandler;

    private readonly HashSet<INotifyPropertyChanged> _attachedItems = [];

    public WorkspaceObservableMutationWatcher(
        ObservableCollection<MappingEntry> mappings,
        ObservableCollection<KeyboardActionDefinition> keyboardActions,
        ObservableCollection<RadialMenuDefinition> radialMenus,
        Action onMutated)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _keyboardActions = keyboardActions ?? throw new ArgumentNullException(nameof(keyboardActions));
        _radialMenus = radialMenus ?? throw new ArgumentNullException(nameof(radialMenus));
        _onMutated = onMutated ?? throw new ArgumentNullException(nameof(onMutated));

        _mappingsHandler = (_, e) => OnMappingsCollectionChanged(e);
        _keyboardHandler = (_, e) => OnKeyboardCollectionChanged(e);
        _radialHandler = (_, e) => OnRadialCollectionChanged(e);
        _itemHandler = (_, _) => _onMutated();

        _mappings.CollectionChanged += _mappingsHandler;
        _keyboardActions.CollectionChanged += _keyboardHandler;
        _radialMenus.CollectionChanged += _radialHandler;

        AttachAllItems(_mappings);
        AttachAllItems(_keyboardActions);
        AttachAllItems(_radialMenus);
    }

    private void OnMappingsCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        HandleCollectionChanged(e, _mappings);

    private void OnKeyboardCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        HandleCollectionChanged(e, _keyboardActions);

    private void OnRadialCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        HandleCollectionChanged(e, _radialMenus);

    private void HandleCollectionChanged<T>(NotifyCollectionChangedEventArgs e, ObservableCollection<T> collection)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    foreach (T o in e.OldItems)
                        DetachItemIfObservable(o);
                }

                if (e.NewItems is not null)
                {
                    foreach (T o in e.NewItems)
                        AttachItemIfObservable(o);
                }

                break;

            case NotifyCollectionChangedAction.Reset:
                DetachAllItems(collection);
                AttachAllItems(collection);
                break;

            case NotifyCollectionChangedAction.Move:
                break;
        }

        _onMutated();
    }

    private void AttachAllItems<T>(ObservableCollection<T> collection)
    {
        foreach (var o in collection)
            AttachItemIfObservable(o);
    }

    private void DetachAllItems<T>(ObservableCollection<T> collection)
    {
        foreach (var o in collection)
            DetachItemIfObservable(o);
    }

    private void AttachItemIfObservable(object? item)
    {
        if (item is not INotifyPropertyChanged notify)
            return;

        if (!_attachedItems.Add(notify))
            return;

        notify.PropertyChanged += _itemHandler;
    }

    private void DetachItemIfObservable(object? item)
    {
        if (item is not INotifyPropertyChanged notify)
            return;

        if (!_attachedItems.Remove(notify))
            return;

        notify.PropertyChanged -= _itemHandler;
    }

    public void Dispose()
    {
        _mappings.CollectionChanged -= _mappingsHandler;
        _keyboardActions.CollectionChanged -= _keyboardHandler;
        _radialMenus.CollectionChanged -= _radialHandler;

        foreach (var n in _attachedItems)
            n.PropertyChanged -= _itemHandler;

        _attachedItems.Clear();
    }
}
