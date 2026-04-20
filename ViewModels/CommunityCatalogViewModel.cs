using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using Gamepad_Mapping.Utils.Community;
using Gamepad_Mapping.Views;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityCatalogViewModel : ObservableObject
{
    private readonly TimeSpan _refreshCooldownDuration;

    private readonly ICommunityTemplateService _communityService;
    private readonly ICommunityTemplateUploadService _uploadService;
    private readonly ICommunityTemplateUploadComplianceService _complianceService;
    private readonly IAppToastService _appToastService;
    private readonly MainViewModel _main;
    private CommunityUploadDialogMemory? _uploadDialogMemory;
    private DateTime _refreshCooldownEndsUtc = DateTime.MinValue;
    private DispatcherTimer? _refreshCooldownTimer;

    [ObservableProperty]
    private bool _isRefreshCooldownActive;

    [ObservableProperty]
    private int _refreshCooldownSecondsRemaining;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    private List<CommunityTemplateInfo>? _cachedCommunityIndex;

    public ObservableCollection<CommunityCatalogFolderGroupViewModel> FolderGroups { get; } = new();

    public CommunityCatalogViewModel(
        MainViewModel main,
        ICommunityTemplateService communityService,
        ICommunityTemplateUploadService uploadService,
        ICommunityTemplateUploadComplianceService complianceService,
        IAppToastService appToastService,
        int communityCatalogRefreshCooldownSeconds = 10)
    {
        _main = main;
        _communityService = communityService;
        _uploadService = uploadService;
        _complianceService = complianceService;
        _appToastService = appToastService;
        var seconds = Math.Clamp(communityCatalogRefreshCooldownSeconds, 0, 600);
        _refreshCooldownDuration = TimeSpan.FromSeconds(seconds);

        if (AppUiLocalization.TryTranslationService() is { } loc)
            loc.PropertyChanged += OnTranslationServicePropertyChanged;
    }

    private void OnTranslationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(TranslationService.Culture) or "Item[]"))
            return;

        OnPropertyChanged(nameof(RefreshButtonToolTip));
        if (_cachedCommunityIndex is null || IsLoading)
            return;

        _ = ApplySearchAndPopulateAsync();
    }

    public string RefreshButtonToolTip
        => IsRefreshCooldownActive
            ? string.Format(
                Localize("CommunityCatalog_RefreshCooldownTooltip"),
                Math.Max(0, RefreshCooldownSecondsRemaining))
            : Localize("Refresh");

    [RelayCommand(CanExecute = nameof(CanRefreshTemplates))]
    public async Task RefreshTemplatesAsync()
    {
        if (IsRefreshCooldownEnabled && DateTime.UtcNow < _refreshCooldownEndsUtc)
            return;

        if (IsRefreshCooldownEnabled)
        {
            _refreshCooldownEndsUtc = DateTime.UtcNow.Add(_refreshCooldownDuration);
            StartRefreshCooldownTimer();
        }

        IsLoading = true;
        StatusMessage = Localize("CommunityCatalog_LoadingTemplates");
        FolderGroups.Clear();
        _cachedCommunityIndex = null;
        RefreshTemplatesCommand.NotifyCanExecuteChanged();

        try
        {
            var templates = await _communityService.GetCommunityIndexSnapshotAsync(
                CancellationToken.None,
                CommunityIndexFetchBehavior.PreferFreshIndex);

            if (templates is null)
            {
                StatusMessage = Localize("CommunityCatalog_LoadFailed");
                return;
            }

            _cachedCommunityIndex = templates;
            await ApplySearchAndPopulateAsync();
        }
        catch
        {
            StatusMessage = Localize("CommunityCatalog_LoadFailed");
        }
        finally
        {
            IsLoading = false;
            RefreshTemplatesCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (IsLoading || _cachedCommunityIndex is null)
            return;

        _ = ApplySearchAndPopulateAsync();
    }

    private async Task ApplySearchAndPopulateAsync()
    {
        if (_cachedCommunityIndex is null)
            return;

        var filtered = CommunityTemplateIndexSearch.Filter(_cachedCommunityIndex, SearchQuery);
        PopulateFolderGroupsFromTemplates(filtered);
        await RefreshLocalInstallFlagsAsync();
        UpdateCatalogStatusMessage(filtered.Count);
    }

    private void PopulateFolderGroupsFromTemplates(IReadOnlyList<CommunityTemplateInfo> templates)
    {
        FolderGroups.Clear();

        var groupedTemplates = templates
            .GroupBy(t => ResolveFolderName(t.CatalogFolder))
            .OrderBy(static g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var folderGroup in groupedTemplates)
        {
            var authorGroups = new ObservableCollection<CommunityCatalogAuthorGroupViewModel>(
                folderGroup
                    .GroupBy(t => ResolveAuthorName(t.Author))
                    .OrderBy(static g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(static authorGroup =>
                    {
                        var templatesByName = new ObservableCollection<CommunityCatalogTemplateItemViewModel>(
                            authorGroup
                                .OrderBy(static t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                                .Select(static t => new CommunityCatalogTemplateItemViewModel(t)));
                        return new CommunityCatalogAuthorGroupViewModel(authorGroup.Key, templatesByName);
                    }));

            FolderGroups.Add(new CommunityCatalogFolderGroupViewModel(folderGroup.Key, authorGroups));
        }
    }

    private void UpdateCatalogStatusMessage(int filteredCount)
    {
        if (_cachedCommunityIndex is null)
            return;

        if (_cachedCommunityIndex.Count == 0)
            StatusMessage = Localize("CommunityCatalog_Empty");
        else if (filteredCount == 0 && !string.IsNullOrWhiteSpace(SearchQuery))
            StatusMessage = Localize("CommunityCatalog_SearchNoResults");
        else
            StatusMessage = null;
    }

    private bool CanRefreshTemplates() => !IsLoading && !IsRefreshCooldownActive;

    private bool IsRefreshCooldownEnabled => _refreshCooldownDuration > TimeSpan.Zero;

    private void StartRefreshCooldownTimer()
    {
        if (_refreshCooldownTimer is not null)
        {
            _refreshCooldownTimer.Stop();
            _refreshCooldownTimer.Tick -= OnRefreshCooldownTick;
            _refreshCooldownTimer = null;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        void StartLocal()
        {
            RefreshCooldownSecondsRemaining = (int)Math.Ceiling((_refreshCooldownEndsUtc - DateTime.UtcNow).TotalSeconds);
            if (RefreshCooldownSecondsRemaining < 1)
                RefreshCooldownSecondsRemaining = (int)_refreshCooldownDuration.TotalSeconds;

            IsRefreshCooldownActive = true;

            _refreshCooldownTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _refreshCooldownTimer.Tick += OnRefreshCooldownTick;
            _refreshCooldownTimer.Start();
        }

        if (dispatcher.CheckAccess())
            StartLocal();
        else
            dispatcher.Invoke(StartLocal);
    }

    private void OnRefreshCooldownTick(object? sender, EventArgs e)
    {
        var left = (int)Math.Ceiling((_refreshCooldownEndsUtc - DateTime.UtcNow).TotalSeconds);
        if (left <= 0)
        {
            StopRefreshCooldownTimer();
            return;
        }

        RefreshCooldownSecondsRemaining = left;
    }

    private void StopRefreshCooldownTimer()
    {
        if (_refreshCooldownTimer is not null)
        {
            _refreshCooldownTimer.Stop();
            _refreshCooldownTimer.Tick -= OnRefreshCooldownTick;
            _refreshCooldownTimer = null;
        }

        IsRefreshCooldownActive = false;
        RefreshCooldownSecondsRemaining = 0;
    }

    partial void OnIsRefreshCooldownActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(RefreshButtonToolTip));
        RefreshTemplatesCommand.NotifyCanExecuteChanged();
    }

    partial void OnRefreshCooldownSecondsRemainingChanged(int value) => OnPropertyChanged(nameof(RefreshButtonToolTip));

    [RelayCommand(CanExecute = nameof(CanDownloadTemplate))]
    public async Task DownloadTemplateAsync(CommunityCatalogTemplateItemViewModel templateItem)
    {
        if (templateItem?.Template == null) return;
        var template = templateItem.Template;

        IsLoading = true;
        StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloading"), template.DisplayName);
        DownloadTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();

        try
        {
            if (templateItem.IsInstalledLocally)
            {
                var precheck = await _communityService.CheckLocalTemplateConflictAsync(template);
                var message = precheck.HasSameFolderIdAndName
                    ? string.Format(
                        Localize("CommunityCatalog_OverwriteWarningExact"),
                        template.DisplayName)
                    : string.Format(
                        Localize("CommunityCatalog_OverwriteWarning"),
                        template.DisplayName);
                var result = MessageBox.Show(
                    message,
                    Localize("CommunityCatalog_OverwriteTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloadCanceled"), template.DisplayName);
                    return;
                }
            }

            var outcome = await _communityService.DownloadTemplateAsync(template, allowOverwrite: true);
            if (outcome.Success)
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloaded"), template.DisplayName);
                templateItem.IsInstalledLocally = true;
                _main.RefreshTemplates(template.Id);
                DeleteTemplateCommand.NotifyCanExecuteChanged();
                ShowToast(
                    Localize("CommunityCatalog_DownloadToastTitle"),
                    string.Format(Localize("CommunityCatalog_DownloadToastMessage"), template.DisplayName));
            }
            else if (outcome.ThrottleReason != CommunityTemplateDownloadThrottleReason.None)
            {
                var waitMinutes = Math.Max(1, (outcome.RetryAfterSeconds + 59) / 60);
                var message = outcome.ThrottleReason == CommunityTemplateDownloadThrottleReason.HourlyDownloadQuota
                    ? string.Format(Localize("CommunityCatalog_DownloadRateLimitedHourly"), waitMinutes)
                    : string.Format(
                        Localize("CommunityCatalog_DownloadRateLimitedMinInterval"),
                        Math.Max(1, outcome.RetryAfterSeconds));
                StatusMessage = message;
                ShowToast(Localize("CommunityCatalog_DownloadRateLimitedToastTitle"), message);
            }
            else
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloadFailed"), template.DisplayName);
                ShowToast(
                    Localize("CommunityCatalog_DownloadFailedToastTitle"),
                    string.Format(Localize("CommunityCatalog_DownloadFailedToastMessage"), template.DisplayName));
            }
        }
        catch
        {
            StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloadError"), template.DisplayName);
        }
        finally
        {
            IsLoading = false;
            DownloadTemplateCommand.NotifyCanExecuteChanged();
            DeleteTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDownloadTemplate(CommunityCatalogTemplateItemViewModel? templateItem) => !IsLoading && templateItem?.Template is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteTemplate))]
    public async Task DeleteTemplateAsync(CommunityCatalogTemplateItemViewModel templateItem)
    {
        if (templateItem?.Template == null) return;

        var template = templateItem.Template;
        var confirm = MessageBox.Show(
            string.Format(Localize("CommunityCatalog_DeleteConfirmMessage"), template.DisplayName),
            Localize("CommunityCatalog_DeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        IsLoading = true;
        StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleting"), template.DisplayName);
        DownloadTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();

        try
        {
            var deleted = await _communityService.DeleteLocalTemplateAsync(template);
            if (deleted)
            {
                templateItem.IsInstalledLocally = false;
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleted"), template.DisplayName);
                _main.RefreshTemplates();
                DeleteTemplateCommand.NotifyCanExecuteChanged();
                ShowToast(
                    Localize("CommunityCatalog_DeleteToastTitle"),
                    string.Format(Localize("CommunityCatalog_DeleteToastMessage"), template.DisplayName));
            }
            else
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleteNotFound"), template.DisplayName);
                ShowToast(
                    Localize("CommunityCatalog_DeleteFailedToastTitle"),
                    string.Format(Localize("CommunityCatalog_DeleteFailedToastMessage"), template.DisplayName));
            }
        }
        catch
        {
            StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleteError"), template.DisplayName);
            ShowToast(
                Localize("CommunityCatalog_DeleteFailedToastTitle"),
                string.Format(Localize("CommunityCatalog_DeleteFailedToastMessage"), template.DisplayName));
        }
        finally
        {
            IsLoading = false;
            DownloadTemplateCommand.NotifyCanExecuteChanged();
            DeleteTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDeleteTemplate(CommunityCatalogTemplateItemViewModel? templateItem)
        => !IsLoading && templateItem?.IsInstalledLocally == true;

    [RelayCommand(CanExecute = nameof(CanUploadToCommunity))]
    private async Task UploadToCommunityAsync()
    {
        var sel = _main.SelectedTemplate;
        if (sel is null)
        {
            MessageBox.Show(
                Localize("CommunityCatalog_UploadSelectTemplateFirst"),
                Localize("CommunityUpload_WindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var collector = new CommunityTemplateBundleCollector(_main.GetProfileService());
        IReadOnlyList<CommunityTemplateBundleEntry> bundleEntries;
        try
        {
            bundleEntries = collector.CollectLinkedTemplates(sel.StorageKey);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(Localize("CommunityCatalog_UploadCollectFailed"), ex.Message),
                Localize("CommunityUpload_WindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (bundleEntries.Count == 0)
        {
            MessageBox.Show(
                Localize("CommunityCatalog_UploadNoTemplates"),
                Localize("CommunityUpload_WindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var primaryTemplate = _main.GetProfileService().LoadSelectedTemplate(sel);
        var publishedIndex = await _communityService
            .GetCommunityIndexSnapshotAsync(CancellationToken.None, CommunityIndexFetchBehavior.PreferFreshIndex)
            .ConfigureAwait(true);
        var bundleFingerprint = CommunityUploadBundleFingerprint.Compute(bundleEntries);
        var dialogVm = new CommunityTemplateUploadDialogViewModel(
            _complianceService,
            publishedIndex,
            ct => _communityService.GetCommunityIndexSnapshotAsync(ct, CommunityIndexFetchBehavior.PreferFreshIndex));
        try
        {
            dialogVm.LoadBundle(bundleEntries);
            if (_uploadDialogMemory?.Matches(bundleFingerprint) == true)
            {
                dialogVm.ApplyDraft(_uploadDialogMemory.Draft);
            }
            else
            {
                dialogVm.GameFolderName = CommunityTemplateUploadDialogViewModel.GuessGameFolder(sel.CatalogSubfolder);
                dialogVm.AuthorName = string.IsNullOrWhiteSpace(sel.Author) ? string.Empty : sel.Author;
                dialogVm.ListingDescription = (primaryTemplate?.CommunityListingDescription ?? string.Empty).Trim();
            }

            var dialog = new CommunityTemplateUploadWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = dialogVm
            };

            if (dialog.ShowDialog() != true)
            {
                _uploadDialogMemory = new CommunityUploadDialogMemory(
                    bundleFingerprint,
                    dialogVm.CaptureDraft());
                return;
            }

            _uploadDialogMemory = new CommunityUploadDialogMemory(
                bundleFingerprint,
                dialogVm.CaptureDraft());

            IsLoading = true;
            UploadToCommunityCommand.NotifyCanExecuteChanged();
            StatusMessage = Localize("CommunityCatalog_StatusUploading");

            try
            {
                var result = await _uploadService.SubmitBundleAsync(
                    dialogVm.GetSelectedTemplates(),
                    dialogVm.GameFolderName,
                    dialogVm.AuthorName,
                    dialogVm.ListingDescription);

                if (result.Success)
                {
                    StatusMessage = string.IsNullOrWhiteSpace(result.PullRequestHtmlUrl)
                        ? Localize("CommunityCatalog_StatusPullRequestCreated")
                        : string.Format(Localize("CommunityCatalog_StatusPullRequestWithUrl"), result.PullRequestHtmlUrl);
                    _uploadDialogMemory = null;
                }
                else
                {
                    var failureMessage = BuildUploadFailureMessage(result);
                    StatusMessage = failureMessage;
                    MessageBox.Show(
                        failureMessage,
                        Localize("CommunityUpload_WindowTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusUploadFailed"), ex.Message);
                MessageBox.Show(ex.Message, Localize("CommunityUpload_WindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                UploadToCommunityCommand.NotifyCanExecuteChanged();
            }
        }
        finally
        {
            dialogVm.Dispose();
        }
    }

    private bool CanUploadToCommunity() => !IsLoading;

    private async Task RefreshLocalInstallFlagsAsync()
    {
        var allTemplateItems = FolderGroups
            .SelectMany(static folder => folder.AuthorGroups)
            .SelectMany(static author => author.Templates)
            .ToList();

        foreach (var templateItem in allTemplateItems)
            templateItem.IsInstalledLocally = await _communityService.IsTemplateDownloadedAsync(templateItem.Template);

        DeleteTemplateCommand.NotifyCanExecuteChanged();
    }

    private static string Localize(string key) => AppUiLocalization.GetString(key);

    private void ShowToast(string title, string message)
    {
        _appToastService.Show(new GamepadMapperGUI.Models.AppToastRequest
        {
            Title = title,
            Message = message,
            AutoHideSeconds = 5
        });
    }

    private string ResolveFolderName(string? catalogFolder)
    {
        var normalized = catalogFolder?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? Localize("CommunityCatalog_UncategorizedFolder") : normalized;
    }

    private string ResolveAuthorName(string? author)
    {
        var normalized = author?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? Localize("CommunityCatalog_UnknownAuthor") : normalized;
    }

    private static string BuildUploadFailureMessage(CommunityTemplateUploadResult result)
    {
        if (!result.IsPipelineBusy)
            return result.ErrorMessage ?? AppUiLocalization.GetString("CommunityUpload_Error_UploadFailedGeneric");

        var busyMessage = Localize("CommunityUpload_Error_PipelineBusy");
        var detail = (result.ErrorMessage ?? string.Empty).Trim();
        return detail.Length == 0
            ? busyMessage
            : string.Concat(busyMessage, Environment.NewLine, Environment.NewLine, detail);
    }
}

