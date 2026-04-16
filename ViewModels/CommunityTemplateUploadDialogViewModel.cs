using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Infrastructure;
using Gamepad_Mapping.Utils;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityTemplateUploadDialogViewModel : ObservableObject, IDisposable
{
    private readonly ICommunityTemplateUploadComplianceService _complianceService;
    private readonly IReadOnlyList<CommunityTemplateInfo>? _publishedCommunityIndex;
    private readonly Debouncer _complianceRefreshDebouncer = new(TimeSpan.FromMilliseconds(1000));
    private readonly TranslationService? _loc;
    private bool _isBulkUpdatingSelection;

    public CommunityTemplateUploadDialogViewModel(
        ICommunityTemplateUploadComplianceService complianceService,
        IReadOnlyList<CommunityTemplateInfo>? publishedCommunityIndex = null)
    {
        _complianceService = complianceService;
        _publishedCommunityIndex = publishedCommunityIndex;
        _loc = Application.Current?.Resources["Loc"] as TranslationService;
        if (_loc is not null)
            _loc.PropertyChanged += OnLocPropertyChanged;
    }

    [ObservableProperty]
    private string _gameFolderName = string.Empty;

    [ObservableProperty]
    private string _authorName = string.Empty;

    [ObservableProperty]
    private string _listingDescription = string.Empty;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    [ObservableProperty]
    private bool _complianceReady;

    public ObservableCollection<CommunityTemplateUploadBundleRowViewModel> BundleItems { get; } = new();

    public ObservableCollection<CommunityTemplateComplianceStepViewModel> ComplianceSteps { get; } = new();

    public string AuthorNameCounterText =>
        string.Format(
            CultureInfo.CurrentCulture,
            "{0}/{1}",
            AuthorName.Length,
            CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength);

    public string ListingDescriptionCounterText =>
        string.Format(
            CultureInfo.CurrentCulture,
            "{0}/{1}",
            ListingDescription.Length,
            CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters);

    public void ApplyDraft(CommunityUploadDialogDraft draft)
    {
        if (draft is null)
            return;

        _isBulkUpdatingSelection = true;
        try
        {
            GameFolderName = draft.GameFolderName ?? string.Empty;
            AuthorName = draft.AuthorName ?? string.Empty;
            ListingDescription = draft.ListingDescription ?? string.Empty;

            var included = new HashSet<string>(
                draft.IncludedStorageKeys ?? [],
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in BundleItems)
                item.IsIncluded = included.Contains(item.StorageKey);
        }
        finally
        {
            _isBulkUpdatingSelection = false;
        }

        UpdateSelectionSummary();
        RequestComplianceRefresh(immediate: true);
    }

    public CommunityUploadDialogDraft CaptureDraft()
    {
        var includedStorageKeys = BundleItems
            .Where(static item => item.IsIncluded)
            .Select(static item => item.StorageKey)
            .ToList();
        return new CommunityUploadDialogDraft(
            GameFolderName ?? string.Empty,
            AuthorName ?? string.Empty,
            ListingDescription ?? string.Empty,
            includedStorageKeys);
    }

    public void LoadBundle(IReadOnlyList<CommunityTemplateBundleEntry> entries)
    {
        BundleItems.Clear();
        foreach (var e in entries)
        {
            BundleItems.Add(new CommunityTemplateUploadBundleRowViewModel(
                e.StorageKey,
                e.Template,
                OnBundleItemChanged));
        }

        UpdateSelectionSummary();
        RequestComplianceRefresh(immediate: true);
    }

    private void OnLocPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GamepadMapperGUI.Services.Infrastructure.TranslationService.Culture) or "Item[]")
        {
            UpdateSelectionSummary();
            RequestComplianceRefresh(immediate: true);
        }
    }

    private void OnBundleItemChanged()
    {
        UpdateSelectionSummary();
        RequestComplianceRefresh();
    }

    public IReadOnlyList<GameProfileTemplate> GetSelectedTemplates()
    {
        return BundleItems.Where(static i => i.IsIncluded).Select(static i => i.Template).ToList();
    }

    [RelayCommand]
    private void SelectAllTemplates()
    {
        _isBulkUpdatingSelection = true;
        try
        {
            foreach (var item in BundleItems)
                item.IsIncluded = true;
        }
        finally
        {
            _isBulkUpdatingSelection = false;
        }

        UpdateSelectionSummary();
        RequestComplianceRefresh(immediate: true);
    }

    [RelayCommand]
    private void ClearTemplateSelection()
    {
        _isBulkUpdatingSelection = true;
        try
        {
            foreach (var item in BundleItems)
                item.IsIncluded = false;
        }
        finally
        {
            _isBulkUpdatingSelection = false;
        }

        UpdateSelectionSummary();
        RequestComplianceRefresh(immediate: true);
    }

    private void UpdateSelectionSummary()
    {
        var n = BundleItems.Count(static i => i.IsIncluded);
        var t = BundleItems.Count;
        var loc = Application.Current?.Resources["Loc"] as GamepadMapperGUI.Services.Infrastructure.TranslationService;
        SelectionSummary = loc is null
            ? string.Format(CultureInfo.CurrentCulture, "{0} of {1} template file(s) selected for upload.", n, t)
            : string.Format(CultureInfo.CurrentCulture, loc["CommunityUpload_SelectionSummary"], n, t);
    }

    partial void OnGameFolderNameChanged(string value) => RequestComplianceRefresh();

    partial void OnAuthorNameChanged(string value)
    {
        OnPropertyChanged(nameof(AuthorNameCounterText));
        RequestComplianceRefresh();
    }

    partial void OnListingDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(ListingDescriptionCounterText));
        RequestComplianceRefresh();
    }

    private static string FormatComplianceIssueDetail(
        CommunityTemplateComplianceIssue issue,
        GamepadMapperGUI.Services.Infrastructure.TranslationService loc)
    {
        if (string.IsNullOrEmpty(issue.DetailResourceKey))
            return issue.Detail;

        var fmt = loc[issue.DetailResourceKey!];
        if (issue.DetailFormatArguments is { Count: > 0 })
            return string.Format(CultureInfo.CurrentCulture, fmt, issue.DetailFormatArguments.ToArray());

        return fmt;
    }

    private void RequestComplianceRefresh(bool immediate = false)
    {
        if (_isBulkUpdatingSelection)
            return;

        if (immediate)
        {
            _complianceRefreshDebouncer.Cancel();
            RefreshCompliance();
            return;
        }

        _complianceRefreshDebouncer.Debounce(RefreshCompliance);
    }

    private void RefreshCompliance()
    {
        var loc = Application.Current?.Resources["Loc"] as GamepadMapperGUI.Services.Infrastructure.TranslationService;
        if (loc is null)
        {
            ComplianceReady = false;
            ComplianceSteps.Clear();
            return;
        }

        var selected = BundleItems
            .Where(static i => i.IsIncluded)
            .Select(static i => new CommunityTemplateBundleEntry(i.StorageKey, i.Template))
            .ToList();

        var result = _complianceService.EvaluateSubmission(
            selected,
            GameFolderName,
            AuthorName,
            ListingDescription);

        ComplianceReady = result.ReadyToSubmit;
        ComplianceSteps.Clear();
        foreach (var step in result.Steps)
        {
            var items = new ObservableCollection<CommunityTemplateComplianceIssueViewModel>();
            foreach (var issue in step.Issues)
            {
                var detail = FormatComplianceIssueDetail(issue, loc);
                var line = string.IsNullOrEmpty(issue.TemplateLabel)
                    ? detail
                    : $"{issue.TemplateLabel}: {detail}";
                var sug = string.IsNullOrEmpty(issue.SuggestionKey)
                    ? null
                    : loc[issue.SuggestionKey!];
                items.Add(new CommunityTemplateComplianceIssueViewModel(line, sug));
            }

            var statusKey = step.Severity switch
            {
                CommunityTemplateComplianceSeverity.Ok => "CommunityUpload_SeveritySummary_Ok",
                CommunityTemplateComplianceSeverity.Warning => "CommunityUpload_SeveritySummary_Warning",
                _ => "CommunityUpload_SeveritySummary_Error"
            };

            ComplianceSteps.Add(new CommunityTemplateComplianceStepViewModel(
                loc[step.TitleKey],
                loc[step.PromptKey],
                loc[statusKey],
                step.Severity,
                items));
        }
    }

    public bool TryCommit(out string? errorMessage)
    {
        errorMessage = null;
        _complianceRefreshDebouncer.Cancel();
        RefreshCompliance();
        var loc = Application.Current?.Resources["Loc"] as GamepadMapperGUI.Services.Infrastructure.TranslationService;

        if (!ComplianceReady)
        {
            errorMessage = loc?["CommunityUpload_FixIssuesBeforeUpload"]
                           ?? "Fix the issues listed under Repository checks before uploading, or cancel.";
            return false;
        }

        var game = (GameFolderName ?? string.Empty).Trim();
        var author = (AuthorName ?? string.Empty).Trim();
        var desc = (ListingDescription ?? string.Empty).Trim();

        if (BundleItems.Count == 0)
        {
            errorMessage = loc?["CommunityUpload_Error_NoTemplatesInBundle"] ?? "No templates in bundle.";
            return false;
        }

        if (!BundleItems.Any(static i => i.IsIncluded))
        {
            errorMessage = loc?["CommunityUpload_Error_SelectAtLeastOne"] ?? "Select at least one template to upload.";
            return false;
        }

        if (game.Length == 0)
        {
            errorMessage = loc?["CommunityUpload_Error_GameFolderRequired"] ?? "Game folder name is required.";
            return false;
        }

        if (author.Length == 0)
        {
            errorMessage = loc?["CommunityUpload_Error_AuthorRequired"] ?? "Author name is required.";
            return false;
        }

        if (desc.Length == 0)
        {
            errorMessage = loc?["CommunityUpload_Error_ListingDescriptionRequired"] ?? "Listing description is required.";
            return false;
        }

        string gameSeg;
        string authorSeg;
        try
        {
            gameSeg = TemplateStorageKey.ValidateSingleSegmentFolderForSave(game);
            authorSeg = TemplateStorageKey.ValidateSingleSegmentFolderForSave(author);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        var catalogFolder = TemplateStorageKey.ValidateCatalogFolderPathForSave($"{gameSeg}/{authorSeg}");
        if (_publishedCommunityIndex is not null)
        {
            var publishedPaths = CommunityTemplateUploadPathConflictChecker.BuildPublishedPathSet(_publishedCommunityIndex);
            var conflicts = CommunityTemplateUploadPathConflictChecker.FindConflictingRelativePaths(
                GetSelectedTemplates(),
                catalogFolder,
                publishedPaths);
            if (conflicts.Count > 0)
            {
                var pathsText = string.Join(Environment.NewLine, conflicts);
                var fmt = loc?["CommunityUpload_Error_PathAlreadyPublished"]
                          ?? "These paths already exist in the community repository. Change the game folder, author folder, or profile id, then try again.\n\n{0}";
                errorMessage = string.Format(CultureInfo.CurrentCulture, fmt, pathsText);
                return false;
            }
        }

        return true;
    }

    public static string GuessGameFolder(string? catalogSubfolder)
    {
        var segments = TemplateStorageKey.SplitCatalogPathSegments(catalogSubfolder ?? string.Empty);
        return segments.Count > 0 ? segments[0] : string.Empty;
    }

    public void Dispose()
    {
        _complianceRefreshDebouncer.Cancel();
        if (_loc is not null)
            _loc.PropertyChanged -= OnLocPropertyChanged;
    }
}
