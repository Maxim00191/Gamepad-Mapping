using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using GamepadMapperGUI.Views;
using System.Windows;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityUploadTicketTokenProvider : ICommunityUploadTicketTokenProvider
{
    private readonly AppSettings _settings;
    private readonly IWebView2RuntimeAvailability _webView2Runtime;
    private readonly Func<string, string> _localize;
    private readonly IUserDialogService _userDialogService;

    public CommunityUploadTicketTokenProvider(
        AppSettings settings,
        IWebView2RuntimeAvailability? webView2Runtime = null,
        Func<string, string>? localizeString = null,
        IUserDialogService? userDialogService = null)
    {
        _settings = settings;
        _webView2Runtime = webView2Runtime ?? new WebView2RuntimeAvailability();
        _localize = localizeString ?? (k => k);
        _userDialogService = userDialogService ?? new UserDialogService();
    }

    public Task<string?> GetTurnstileTokenAsync(CancellationToken cancellationToken = default)
    {
        var siteKey = (_settings.CommunityProfilesUploadTurnstileSiteKey ?? string.Empty).Trim();
        if (siteKey.Length == 0)
            return Task.FromResult<string?>(null);

        var action = (_settings.CommunityProfilesUploadTurnstileAction ?? string.Empty).Trim();
        if (action.Length == 0)
            action = "community_upload_ticket";

        if (!TryResolveTurnstileHostPageBase(_settings, out var hostPageBase)
            || !TryBuildChallengePageUri(hostPageBase, siteKey, action, out var challengeUri))
        {
            return Task.FromResult<string?>(null);
        }

        var app = Application.Current;
        if (app is null)
            return Task.FromResult<string?>(null);

        return app.Dispatcher.InvokeAsync(
            () => GetTokenOnUiThreadAsync(challengeUri, cancellationToken),
            System.Windows.Threading.DispatcherPriority.Normal,
            cancellationToken).Task.Unwrap();
    }

    private static bool TryResolveTurnstileHostPageBase(AppSettings settings, out string hostPageBase)
    {
        var explicitUrl = (settings.CommunityProfilesUploadTurnstileHostPageUrl ?? string.Empty).Trim();
        if (explicitUrl.Length > 0)
        {
            hostPageBase = explicitUrl;
            return true;
        }

        var worker = (settings.CommunityProfilesUploadWorkerUrl ?? string.Empty).Trim();
        if (worker.Length == 0 || !Uri.TryCreate(worker, UriKind.Absolute, out var workerUri))
        {
            hostPageBase = string.Empty;
            return false;
        }

        if (workerUri.Scheme != Uri.UriSchemeHttps)
        {
            hostPageBase = string.Empty;
            return false;
        }

        var ub = new UriBuilder(workerUri.Scheme, workerUri.Host, workerUri.Port, "/turnstile-embed");
        hostPageBase = ub.Uri.AbsoluteUri;
        return true;
    }

    private static bool TryBuildChallengePageUri(string hostPageBase, string siteKey, string action, out Uri uri)
    {
        uri = default!;
        if (!Uri.TryCreate(hostPageBase, UriKind.Absolute, out var baseUri))
            return false;

        if (baseUri.Scheme != Uri.UriSchemeHttps
            && !(baseUri.Scheme == Uri.UriSchemeHttp
                 && IsLoopbackHost(baseUri.Host)))
        {
            return false;
        }

        var ub = new UriBuilder(baseUri);
        var existing = ub.Query.TrimStart('?');
        var parts = new List<string>();
        if (existing.Length > 0)
        {
            foreach (var segment in existing.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.StartsWith("siteKey=", StringComparison.OrdinalIgnoreCase)
                    || segment.StartsWith("action=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                parts.Add(segment);
            }
        }

        parts.Add("siteKey=" + Uri.EscapeDataString(siteKey));
        parts.Add("action=" + Uri.EscapeDataString(action));
        ub.Query = string.Join("&", parts);
        uri = ub.Uri;
        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrEmpty(host))
            return false;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
               || string.Equals(host, "[::1]", StringComparison.Ordinal);
    }

    private async Task<string?> GetTokenOnUiThreadAsync(Uri challengePageUri, CancellationToken cancellationToken)
    {
        if (!_webView2Runtime.IsRuntimeInstalled())
        {
            var owner = Application.Current?.MainWindow;
            var title = _localize("WebView2_RuntimeRequired_Title");
            var message = _localize("WebView2_RuntimeRequired_Message");
            var result = _userDialogService.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                owner: owner);
            if (result == MessageBoxResult.Yes)
                TryOpenWebView2DownloadPage();

            return null;
        }

        var ownerWindow = Application.Current?.MainWindow;
        var challengeWindow = new TurnstileChallengeWindow(challengePageUri)
        {
            Owner = ownerWindow
        };

        using var registration = cancellationToken.Register(() =>
        {
            var dispatcher = challengeWindow.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                if (challengeWindow.IsVisible)
                    challengeWindow.Close();
                return;
            }

            _ = dispatcher.BeginInvoke(new Action(() =>
            {
                if (challengeWindow.IsVisible)
                    challengeWindow.Close();
            }));
        });

        challengeWindow.Show();
        return await challengeWindow.WaitForTokenAsync().ConfigureAwait(true);
    }

    private static void TryOpenWebView2DownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WebView2RuntimeDownload.EvergreenBootstrapperFwLink,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Gamepad_Mapping.App.Logger.Warning($"Could not open WebView2 download link: {ex.Message}");
        }
    }
}
