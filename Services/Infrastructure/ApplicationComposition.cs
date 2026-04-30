#nullable enable
using System.Windows;
using System.Windows.Threading;
using Gamepad_Mapping;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Centralizes construction of the main window ViewModel and shared startup singletons.
/// Keeps <see cref="App"/> thin and documents the application graph in one place.
/// </summary>
public static class ApplicationComposition
{
    public static (MainViewModel Main, IAppToastService ToastService) BuildMainViewModel()
    {
        var gitHubContentService = new GitHubContentService();
        var localFileService = new LocalFileService();
        var updateInstallerService = new UpdateInstallerService();
        var settingsService = new SettingsService();
        var appSettings = settingsService.LoadSettingsInternal();
        var profileService = new ProfileService(settingsService: settingsService, appSettings: appSettings);
        var profileDomainService = new ProfileDomainService();
        var updateVersionCacheService = new UpdateVersionCacheService();
        var trustedUtcTimeService = new TrustedUtcTimeService();
        var updateQuotaPolicyProvider = new StaticUpdateQuotaPolicyProvider();
        var updateQuotaService = new UpdateQuotaService(updateQuotaPolicyProvider, trustedUtcTimeService);
        var appToastService = new AppToastService();
        var userDialogService = new UserDialogService();
        var xinputService = new XInputService();
        var playStationInputProvider = new DualSenseHidInputProvider(App.Logger);
        var gamepadSourceFactory = new GamepadSourceFactory(xinputService, playStationInputProvider);
        var gamepadSource = gamepadSourceFactory.CreateSource(appSettings.GamepadSourceApi, out _);
        var communityDownloadThrottle = new CommunityTemplateDownloadThrottle();

        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var mainShellVisibility = new MainShellVisibility();
        var uiOrchestrator = new UiOrchestrator(appToastService, dispatcher, mainShellVisibility);

        var mainViewModel = new MainViewModel(
            profileService: profileService,
            gitHubContentService: gitHubContentService,
            communityService: new CommunityTemplateService(
                profileService,
                gitHubContentService,
                localFileService,
                appSettings,
                communityDownloadThrottle),
            updateService: new UpdateService(gitHubContentService, settingsService, appSettings, updateVersionCacheService),
            localFileService: localFileService,
            updateInstallerService: updateInstallerService,
            updateQuotaService: updateQuotaService,
            settingsService: settingsService,
            trustedUtcTimeService: trustedUtcTimeService,
            updateVersionCacheService: updateVersionCacheService,
            updateQuotaPolicyProvider: updateQuotaPolicyProvider,
            appToastService: appToastService,
            userDialogService: userDialogService,
            xinput: xinputService,
            gamepadSource: gamepadSource,
            gamepadSourceFactory: gamepadSourceFactory,
            uiOrchestrator: uiOrchestrator,
            profileDomainService: profileDomainService,
            mainShellVisibility: mainShellVisibility);

        return (mainViewModel, appToastService);
    }
}
