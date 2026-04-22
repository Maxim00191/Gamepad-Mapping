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

public partial class CommunityCatalogViewModel : ObservableObject, IDisposable
{
    private enum CommunityCatalogIndexLoadKind
    {
        AutomaticOnFirstOpen,
        UserManualRefresh,
    }

    private readonly TimeSpan _refreshCooldownDuration;

    private readonly ICommunityTemplateService _communityService;
    private readonly ICommunityTemplateUploadService _uploadService;
    private readonly ICommunityTemplateUploadComplianceService _complianceService;
    private readonly IAppToastService _appToastService;
    private readonly IUserDialogService _userDialogService;
    private readonly MainViewModel _main;
    private readonly TranslationService? _translationService;
    private readonly CancellationTokenSource _lifetimeCts = new();
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
        IUserDialogService userDialogService,
        int communityCatalogRefreshCooldownSeconds = 10)
    {
        _main = main;
        _communityService = communityService;
        _uploadService = uploadService;
        _complianceService = complianceService;
        _appToastService = appToastService;
        _userDialogService = userDialogService;
        var seconds = Math.Clamp(communityCatalogRefreshCooldownSeconds, 0, 600);
        _refreshCooldownDuration = TimeSpan.FromSeconds(seconds);

        _translationService = AppUiLocalization.TryTranslationService();
        if (_translationService is not null)
            _translationService.PropertyChanged += OnTranslationServicePropertyChanged;
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

    /// <summary>
    /// When the user opens the Community workspace tab and no index is in memory yet,
    /// fetches the published index once (same network behavior as refresh, without cooldown).
    /// </summary>
    public Task EnsureCommunityCatalogIndexWhenEmptyAsync()
    {
        if (_cachedCommunityIndex is not null || IsLoading)
            return Task.CompletedTask;

        return FetchAndApplyCommunityIndexAsync(CommunityCatalogIndexLoadKind.AutomaticOnFirstOpen);
    }

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

        await FetchAndApplyCommunityIndexAsync(CommunityCatalogIndexLoadKind.UserManualRefresh);
    }

    private async Task FetchAndApplyCommunityIndexAsync(CommunityCatalogIndexLoadKind kind)
    {
        if (IsLoading)
            return;

        var manual = kind == CommunityCatalogIndexLoadKind.UserManualRefresh;

        IsLoading = true;
        StatusMessage = manual
            ? Localize("CommunityCatalog_LoadingTemplates")
            : Localize("CommunityCatalog_AutoGettingUpdates");

        if (manual)
        {
            FolderGroups.Clear();
            _cachedCommunityIndex = null;
        }

        NotifyRefreshAndTemplateCommands();

        try
        {
            var templates = await _communityService.GetCommunityIndexSnapshotAsync(
                _lifetimeCts.Token,
                CommunityIndexFetchBehavior.PreferFreshIndex);

            if (templates is null)
            {
                StatusMessage = Localize("CommunityCatalog_LoadFailed");
                return;
            }

            _cachedCommunityIndex = templates;
            await ApplySearchAndPopulateAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Gamepad_Mapping.App.Logger.Warning($"Failed to load community catalog index: {ex.Message}");
            StatusMessage = Localize("CommunityCatalog_LoadFailed");
        }
        finally
        {
            IsLoading = false;
            NotifyRefreshAndTemplateCommands();
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        NotifyRefreshAndTemplateCommands();
        UploadToCommunityCommand.NotifyCanExecuteChanged();
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
        PopulateFolderGroupsFromTemplates(filtered, AppUiLocalization.TryTranslationService());
        await RefreshLocalInstallFlagsAsync();
        UpdateCatalogStatusMessage(filtered.Count);
    }

    private void PopulateFolderGroupsFromTemplates(
        IReadOnlyList<CommunityTemplateInfo> templates,
        TranslationService? translationService)
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
                    .Select(authorGroup =>
                    {
                        var templatesByName = new ObservableCollection<CommunityCatalogTemplateItemViewModel>(
                            authorGroup
                                .OrderBy(t => CommunityTemplateDisplayLabels.ResolveDisplayName(t, translationService), StringComparer.OrdinalIgnoreCase)
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
        StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloading"), CommunityTemplateUiTitle(template));
        NotifyTemplateMutationCommands();

        try
        {
            if (templateItem.IsInstalledLocally)
            {
                var precheck = await _communityService.CheckLocalTemplateConflictAsync(template);
                var message = precheck.HasSameFolderIdAndName
                    ? string.Format(
                        Localize("CommunityCatalog_OverwriteWarningExact"),
                        CommunityTemplateUiTitle(template))
                    : string.Format(
                        Localize("CommunityCatalog_OverwriteWarning"),
                        CommunityTemplateUiTitle(template));
                var result = _userDialogService.ConfirmYesNo(
                    message,
                    Localize("CommunityCatalog_OverwriteTitle"),
                    MessageBoxImage.Warning);
                if (!result)
                {
                    StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloadCanceled"), CommunityTemplateUiTitle(template));
                    return;
                }
            }

            var outcome = await _communityService.DownloadTemplateAsync(template, allowOverwrite: true);
            if (outcome.Success)
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloaded"), CommunityTemplateUiTitle(template));
                templateItem.IsInstalledLocally = true;
                _main.RefreshTemplates(template.Id);
                NotifyTemplateMutationCommands();
                ShowToast(
                    Localize("CommunityCatalog_DownloadToastTitle"),
                    string.Format(Localize("CommunityCatalog_DownloadToastMessage"), CommunityTemplateUiTitle(template)));
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
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloadFailed"), CommunityTemplateUiTitle(template));
                ShowToast(
                    Localize("CommunityCatalog_DownloadFailedToastTitle"),
                    string.Format(Localize("CommunityCatalog_DownloadFailedToastMessage"), CommunityTemplateUiTitle(template)));
            }
        }
        catch (Exception ex)
        {
            Gamepad_Mapping.App.Logger.Warning($"Failed to download community template '{template.Id}': {ex.Message}");
            StatusMessage = string.Format(Localize("CommunityCatalog_StatusDownloadError"), CommunityTemplateUiTitle(template));
        }
        finally
        {
            IsLoading = false;
            NotifyTemplateMutationCommands();
        }
    }

    private bool CanDownloadTemplate(CommunityCatalogTemplateItemViewModel? templateItem) => !IsLoading && templateItem?.Template is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteTemplate))]
    public async Task DeleteTemplateAsync(CommunityCatalogTemplateItemViewModel templateItem)
    {
        if (templateItem?.Template == null) return;

        var template = templateItem.Template;
        var confirm = _userDialogService.ConfirmYesNo(
            string.Format(Localize("CommunityCatalog_DeleteConfirmMessage"), CommunityTemplateUiTitle(template)),
            Localize("CommunityCatalog_DeleteConfirmTitle"),
            MessageBoxImage.Warning);

        if (!confirm)
            return;

        IsLoading = true;
        StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleting"), CommunityTemplateUiTitle(template));
        NotifyTemplateMutationCommands();

        try
        {
            var deleted = await _communityService.DeleteLocalTemplateAsync(template);
            if (deleted)
            {
                templateItem.IsInstalledLocally = false;
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleted"), CommunityTemplateUiTitle(template));
                _main.RefreshTemplates();
                NotifyTemplateMutationCommands();
                ShowToast(
                    Localize("CommunityCatalog_DeleteToastTitle"),
                    string.Format(Localize("CommunityCatalog_DeleteToastMessage"), CommunityTemplateUiTitle(template)));
            }
            else
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleteNotFound"), CommunityTemplateUiTitle(template));
                ShowToast(
                    Localize("CommunityCatalog_DeleteFailedToastTitle"),
                    string.Format(Localize("CommunityCatalog_DeleteFailedToastMessage"), CommunityTemplateUiTitle(template)));
            }
        }
        catch (Exception ex)
        {
            Gamepad_Mapping.App.Logger.Warning($"Failed to delete local community template '{template.Id}': {ex.Message}");
            StatusMessage = string.Format(Localize("CommunityCatalog_StatusDeleteError"), CommunityTemplateUiTitle(template));
            ShowToast(
                Localize("CommunityCatalog_DeleteFailedToastTitle"),
                string.Format(Localize("CommunityCatalog_DeleteFailedToastMessage"), CommunityTemplateUiTitle(template)));
        }
        finally
        {
            IsLoading = false;
            NotifyTemplateMutationCommands();
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
            _userDialogService.ShowInfo(
                Localize("CommunityCatalog_UploadSelectTemplateFirst"),
                Localize("CommunityUpload_WindowTitle"));
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
            _userDialogService.ShowWarning(
                string.Format(Localize("CommunityCatalog_UploadCollectFailed"), ex.Message),
                Localize("CommunityUpload_WindowTitle"));
            return;
        }

        if (bundleEntries.Count == 0)
        {
            _userDialogService.ShowInfo(
                Localize("CommunityCatalog_UploadNoTemplates"),
                Localize("CommunityUpload_WindowTitle"));
            return;
        }

        var primaryTemplate = _main.GetProfileService().LoadSelectedTemplate(sel);
        IReadOnlyList<CommunityTemplateInfo>? publishedIndex;
        try
        {
            publishedIndex = await _communityService
                .GetCommunityIndexSnapshotAsync(_lifetimeCts.Token, CommunityIndexFetchBehavior.PreferFreshIndex)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
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

            var dialog = new CommunityTemplateUploadWindow(_userDialogService)
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
                    _userDialogService.ShowWarning(
                        failureMessage,
                        Localize("CommunityUpload_WindowTitle"));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Localize("CommunityCatalog_StatusUploadFailed"), ex.Message);
                _userDialogService.ShowError(ex.Message, Localize("CommunityUpload_WindowTitle"));
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

        var installChecks = allTemplateItems
            .Select(async templateItem => new
            {
                templateItem,
                isInstalled = await _communityService.IsTemplateDownloadedAsync(templateItem.Template).ConfigureAwait(true)
            })
            .ToArray();

        var results = await Task.WhenAll(installChecks).ConfigureAwait(true);
        foreach (var result in results)
            result.templateItem.IsInstalledLocally = result.isInstalled;

        DeleteTemplateCommand.NotifyCanExecuteChanged();
    }

    private static string CommunityTemplateUiTitle(CommunityTemplateInfo template) =>
        CommunityTemplateDisplayLabels.ResolveDisplayName(template, AppUiLocalization.TryTranslationService());

    private static string Localize(string key) => AppUiLocalization.GetString(key);

    private void NotifyRefreshAndTemplateCommands()
    {
        RefreshTemplatesCommand.NotifyCanExecuteChanged();
        NotifyTemplateMutationCommands();
    }

    private void NotifyTemplateMutationCommands()
    {
        DownloadTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();
    }

    private void ShowToast(string title, string message)
    {
        _appToastService.Show(new GamepadMapperGUI.Models.AppToastRequest
        {
            Title = title,
            Message = message,
            AutoHideSeconds = AppToastDefaults.AutoHideSeconds
        });
    }

    public void Dispose()
    {
        if (_translationService is not null)
            _translationService.PropertyChanged -= OnTranslationServicePropertyChanged;
        StopRefreshCooldownTimer();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
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

