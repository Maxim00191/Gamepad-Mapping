using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityTemplateUploadDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _gameFolderName = string.Empty;

    [ObservableProperty]
    private string _authorName = string.Empty;

    [ObservableProperty]
    private string _listingDescription = string.Empty;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    public ObservableCollection<CommunityTemplateUploadBundleRowViewModel> BundleItems { get; } = new();

    public void LoadBundle(IReadOnlyList<CommunityTemplateBundleEntry> entries)
    {
        BundleItems.Clear();
        foreach (var e in entries)
        {
            BundleItems.Add(new CommunityTemplateUploadBundleRowViewModel(
                e.StorageKey,
                e.Template,
                UpdateSelectionSummary));
        }

        UpdateSelectionSummary();
    }

    public IReadOnlyList<GameProfileTemplate> GetSelectedTemplates()
    {
        return BundleItems.Where(static i => i.IsIncluded).Select(static i => i.Template).ToList();
    }

    [RelayCommand]
    private void SelectAllTemplates()
    {
        foreach (var item in BundleItems)
            item.IsIncluded = true;
        UpdateSelectionSummary();
    }

    [RelayCommand]
    private void ClearTemplateSelection()
    {
        foreach (var item in BundleItems)
            item.IsIncluded = false;
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var n = BundleItems.Count(static i => i.IsIncluded);
        var t = BundleItems.Count;
        SelectionSummary = $"{n} of {t} template file(s) selected for upload.";
    }

    public bool TryCommit(out string? errorMessage)
    {
        errorMessage = null;
        var game = (GameFolderName ?? string.Empty).Trim();
        var author = (AuthorName ?? string.Empty).Trim();
        var desc = (ListingDescription ?? string.Empty).Trim();

        if (BundleItems.Count == 0)
        {
            errorMessage = "No templates in bundle.";
            return false;
        }

        if (!BundleItems.Any(static i => i.IsIncluded))
        {
            errorMessage = "Select at least one template to upload.";
            return false;
        }

        if (game.Length == 0)
        {
            errorMessage = "Game folder name is required.";
            return false;
        }

        if (author.Length == 0)
        {
            errorMessage = "Author name is required.";
            return false;
        }

        if (desc.Length == 0)
        {
            errorMessage = "Listing description is required.";
            return false;
        }

        try
        {
            TemplateStorageKey.ValidateSingleSegmentFolderForSave(game);
            TemplateStorageKey.ValidateSingleSegmentFolderForSave(author);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        return true;
    }

    public static string GuessGameFolder(string? catalogSubfolder)
    {
        var segments = TemplateStorageKey.SplitCatalogPathSegments(catalogSubfolder ?? string.Empty);
        return segments.Count > 0 ? segments[0] : string.Empty;
    }
}
