using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Services.Input;

public class SelectionService<T> : ISelectionService<T> where T : class
{
    private T? _selectedItem;
    private readonly List<T> _selectedItems = [];
    private bool _isUpdating;

    public T? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            if (!_isUpdating)
            {
                _selectedItems.Clear();
                if (value != null) _selectedItems.Add(value);
                OnSelectionChanged();
            }
        }
    }

    public IReadOnlyList<T> SelectedItems => _selectedItems;

    public void UpdateSelection(IEnumerable<object> items)
    {
        _isUpdating = true;
        try
        {
            _selectedItems.Clear();
            foreach (var item in items)
            {
                if (item is T t)
                    _selectedItems.Add(t);
            }

            var last = _selectedItems.LastOrDefault();
            if (!ReferenceEquals(_selectedItem, last))
            {
                _selectedItem = last;
            }
            OnSelectionChanged();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public void ResetTo(T? item)
    {
        _isUpdating = true;
        try
        {
            _selectedItems.Clear();
            _selectedItem = item;
            if (item is not null)
                _selectedItems.Add(item);
            OnSelectionChanged();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public void SelectAll(IEnumerable<T> allItems)
    {
        _isUpdating = true;
        try
        {
            _selectedItems.Clear();
            _selectedItems.AddRange(allItems);
            _selectedItem = _selectedItems.LastOrDefault();
            OnSelectionChanged();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public event EventHandler? SelectionChanged;

    protected virtual void OnSelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
