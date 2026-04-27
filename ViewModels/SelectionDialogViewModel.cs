using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Gamepad_Mapping.Models.State;

namespace Gamepad_Mapping.ViewModels;

public partial class SelectionDialogViewModel : ObservableObject
{
    private readonly IReadOnlyList<SelectionDialogItem> _allItems;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _searchPlaceholder = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SelectionDialogItem? _selectedItem;

    public ObservableCollection<SelectionDialogItem> VisibleItems { get; } = [];

    public SelectionDialogViewModel(
        string title,
        string searchPlaceholder,
        IReadOnlyList<SelectionDialogItem> items,
        string? initiallySelectedKey)
    {
        Title = title;
        SearchPlaceholder = searchPlaceholder;
        _allItems = items;
        ApplyFilter();
        if (!string.IsNullOrWhiteSpace(initiallySelectedKey))
            SelectedItem = VisibleItems.FirstOrDefault(i => string.Equals(i.Key, initiallySelectedKey, StringComparison.OrdinalIgnoreCase));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var keyword = (SearchText ?? string.Empty).Trim();
        var filtered = keyword.Length == 0
            ? _allItems
            : _allItems.Where(i => i.SearchText.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        VisibleItems.Clear();
        foreach (var item in filtered)
            VisibleItems.Add(item);

        if (SelectedItem is not null && !VisibleItems.Contains(SelectedItem))
            SelectedItem = VisibleItems.FirstOrDefault();
        else if (SelectedItem is null && VisibleItems.Count > 0)
            SelectedItem = VisibleItems[0];
    }
}
