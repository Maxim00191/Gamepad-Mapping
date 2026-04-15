using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Views;
using System.Windows;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityUploadTicketTokenProvider : ICommunityUploadTicketTokenProvider
{
    private readonly AppSettings _settings;

    public CommunityUploadTicketTokenProvider(AppSettings settings)
    {
        _settings = settings;
    }

    public Task<string?> GetTurnstileTokenAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG
        var manualToken = (_settings.CommunityProfilesUploadTurnstileToken ?? string.Empty).Trim();
        if (manualToken.Length > 0)
            return Task.FromResult<string?>(manualToken);
#endif

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

    private static async Task<string?> GetTokenOnUiThreadAsync(Uri challengePageUri, CancellationToken cancellationToken)
    {
        var owner = Application.Current?.MainWindow;
        var challengeWindow = new TurnstileChallengeWindow(challengePageUri)
        {
            Owner = owner
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
}
