using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Services.Infrastructure;
using Gamepad_Mapping.Views;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityCatalogViewModel : ObservableObject
{
    private readonly ICommunityTemplateService _communityService;
    private readonly ICommunityTemplateUploadService _uploadService;
    private readonly MainViewModel _main;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<CommunityTemplateFolderGroup> FolderGroups { get; } = new();

    public CommunityCatalogViewModel(
        MainViewModel main,
        ICommunityTemplateService communityService,
        ICommunityTemplateUploadService uploadService)
    {
        _main = main;
        _communityService = communityService;
        _uploadService = uploadService;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshTemplates))]
    public async Task RefreshTemplatesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading community templates...";
        FolderGroups.Clear();
        RefreshTemplatesCommand.NotifyCanExecuteChanged();

        try
        {
            var templates = await _communityService.GetTemplatesAsync();

            var groupedTemplates = templates
                .GroupBy(static t => ResolveFolderName(t.CatalogFolder))
                .OrderBy(static g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var folderGroup in groupedTemplates)
            {
                var groupedItems = new ObservableCollection<CommunityTemplateInfo>(
                    folderGroup.OrderBy(static t => t.DisplayName, StringComparer.OrdinalIgnoreCase));
                FolderGroups.Add(new CommunityTemplateFolderGroup(folderGroup.Key, groupedItems));
            }

            StatusMessage = templates.Count > 0 ? null : "No templates found.";
        }
        catch
        {
            StatusMessage = "Failed to load community templates.";
        }
        finally
        {
            IsLoading = false;
            RefreshTemplatesCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRefreshTemplates() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanDownloadTemplate))]
    public async Task DownloadTemplateAsync(CommunityTemplateInfo template)
    {
        if (template == null) return;

        IsLoading = true;
        StatusMessage = $"Downloading {template.DisplayName}...";
        DownloadTemplateCommand.NotifyCanExecuteChanged();

        try
        {
            var precheck = await _communityService.CheckLocalTemplateConflictAsync(template);
            if (precheck.HasSameFolderIdAndName)
            {
                var message =
                    $"A local template with the same folder, id, and name already exists.\n\n" +
                    $"Template: {template.DisplayName}\n\n" +
                    "Do you want to overwrite it with the community version?";
                var result = MessageBox.Show(
                    message,
                    "Overwrite Existing Template",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
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
                // Refresh templates in the main profile selector.
                _main.RefreshTemplates(template.Id);
            }
            else
            {
                StatusMessage = $"Failed to download {template.DisplayName}.";
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
        }
    }

    private bool CanDownloadTemplate(CommunityTemplateInfo? template) => !IsLoading;

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
        var dialogVm = new CommunityTemplateUploadDialogViewModel
        {
            GameFolderName = CommunityTemplateUploadDialogViewModel.GuessGameFolder(sel.CatalogSubfolder),
            AuthorName = string.IsNullOrWhiteSpace(sel.Author) ? string.Empty : sel.Author,
            ListingDescription = (primaryTemplate?.CommunityListingDescription ?? string.Empty).Trim(),
        };
        dialogVm.LoadBundle(bundleEntries);

        var dialog = new CommunityTemplateUploadWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = dialogVm
        };

        if (dialog.ShowDialog() != true)
            return;

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
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Upload failed.";
                MessageBox.Show(
                    result.ErrorMessage ?? "Upload failed.",
                    "Community upload",
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

    private bool CanUploadToCommunity() => !IsLoading;

    private static string ResolveFolderName(string? catalogFolder)
    {
        var normalized = catalogFolder?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Uncategorized" : normalized;
    }
}

