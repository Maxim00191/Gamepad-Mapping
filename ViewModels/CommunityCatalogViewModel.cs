using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using GamepadMapperGUI.Services.Infrastructure;
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
        StatusMessage = "Loading community templates...";
        FolderGroups.Clear();
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

            var groupedTemplates = templates
                .GroupBy(static t => ResolveFolderName(t.CatalogFolder))
                .OrderBy(static g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var folderGroup in groupedTemplates)
            {
                var groupedItems = new ObservableCollection<CommunityCatalogTemplateItemViewModel>(
                    folderGroup
                        .OrderBy(static t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .Select(static t => new CommunityCatalogTemplateItemViewModel(t)));
                FolderGroups.Add(new CommunityCatalogFolderGroupViewModel(folderGroup.Key, groupedItems));
            }

            await RefreshLocalInstallFlagsAsync();
            StatusMessage = templates.Count > 0 ? null : Localize("CommunityCatalog_Empty");
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
        StatusMessage = $"Downloading {template.DisplayName}...";
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
                    StatusMessage = $"Download canceled for {template.DisplayName}.";
                    return;
                }
            }

            var success = await _communityService.DownloadTemplateAsync(template, allowOverwrite: true);
            if (success)
            {
                StatusMessage = $"Successfully downloaded {template.DisplayName}.";
                templateItem.IsInstalledLocally = true;
                _main.RefreshTemplates(template.Id);
                DeleteTemplateCommand.NotifyCanExecuteChanged();
                ShowToast(
                    Localize("CommunityCatalog_DownloadToastTitle"),
                    string.Format(Localize("CommunityCatalog_DownloadToastMessage"), template.DisplayName));
            }
            else
            {
                StatusMessage = $"Failed to download {template.DisplayName}.";
                ShowToast(
                    Localize("CommunityCatalog_DownloadFailedToastTitle"),
                    string.Format(Localize("CommunityCatalog_DownloadFailedToastMessage"), template.DisplayName));
            }
        }
        catch
        {
            StatusMessage = $"Error downloading {template.DisplayName}.";
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
        StatusMessage = $"Deleting {template.DisplayName}...";
        DownloadTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();

        try
        {
            var deleted = await _communityService.DeleteLocalTemplateAsync(template);
            if (deleted)
            {
                templateItem.IsInstalledLocally = false;
                StatusMessage = $"Deleted {template.DisplayName}.";
                _main.RefreshTemplates();
                DeleteTemplateCommand.NotifyCanExecuteChanged();
                ShowToast(
                    Localize("CommunityCatalog_DeleteToastTitle"),
                    string.Format(Localize("CommunityCatalog_DeleteToastMessage"), template.DisplayName));
            }
            else
            {
                StatusMessage = $"Template not found locally: {template.DisplayName}.";
                ShowToast(
                    Localize("CommunityCatalog_DeleteFailedToastTitle"),
                    string.Format(Localize("CommunityCatalog_DeleteFailedToastMessage"), template.DisplayName));
            }
        }
        catch
        {
            StatusMessage = $"Error deleting {template.DisplayName}.";
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
                "Select a template in the profile panel before uploading.",
                "Community upload",
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
                $"Could not collect templates: {ex.Message}",
                "Community upload",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (bundleEntries.Count == 0)
        {
            MessageBox.Show(
                "No templates to upload.",
                "Community upload",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var primaryTemplate = _main.GetProfileService().LoadSelectedTemplate(sel);
        var publishedIndex = await _communityService.GetCommunityIndexSnapshotAsync().ConfigureAwait(true);
        var bundleFingerprint = CommunityUploadBundleFingerprint.Compute(bundleEntries);
        var dialogVm = new CommunityTemplateUploadDialogViewModel(_complianceService, publishedIndex);
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
            StatusMessage = "Uploading to GitHub…";

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
                        ? "Pull request created."
                        : $"Pull request: {result.PullRequestHtmlUrl}";
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
                StatusMessage = $"Upload failed: {ex.Message}";
                MessageBox.Show(ex.Message, "Community upload", MessageBoxButton.OK, MessageBoxImage.Error);
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
            .SelectMany(static g => g.Templates)
            .ToList();

        foreach (var templateItem in allTemplateItems)
            templateItem.IsInstalledLocally = await _communityService.IsTemplateDownloadedAsync(templateItem.Template);

        DeleteTemplateCommand.NotifyCanExecuteChanged();
    }

    private static string Localize(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];

        return key;
    }

    private void ShowToast(string title, string message)
    {
        _appToastService.Show(new GamepadMapperGUI.Models.AppToastRequest
        {
            Title = title,
            Message = message,
            AutoHideSeconds = 5
        });
    }

    private static string ResolveFolderName(string? catalogFolder)
    {
        var normalized = catalogFolder?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Uncategorized" : normalized;
    }

    private static string BuildUploadFailureMessage(CommunityTemplateUploadResult result)
    {
        if (!result.IsPipelineBusy)
            return result.ErrorMessage ?? "Upload failed.";

        var busyMessage = Localize("CommunityUpload_Error_PipelineBusy");
        var detail = (result.ErrorMessage ?? string.Empty).Trim();
        return detail.Length == 0
            ? busyMessage
            : string.Concat(busyMessage, Environment.NewLine, Environment.NewLine, detail);
    }
}

