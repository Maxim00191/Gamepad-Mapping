using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace GamepadMapperGUI.Views;

public sealed class TurnstileChallengeWindow : Window
{
    private const string CallbackScheme = "gamepadmapping-turnstile";
    private readonly TaskCompletionSource<string?> _tokenSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Uri _challengePageUri;
    private readonly WebView2 _webView;
    private bool _completed;
    private bool _initialized;

    public TurnstileChallengeWindow(Uri challengePageUri)
    {
        ArgumentNullException.ThrowIfNull(challengePageUri);
        if (!IsAllowedChallengePageUri(challengePageUri))
        {
            throw new ArgumentException(
                "Challenge page must be HTTPS, or HTTP with a loopback host (e.g. local dev).",
                nameof(challengePageUri));
        }

        _challengePageUri = challengePageUri;

        Title = "Verify upload";
        Width = 480;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        _webView = new WebView2();
        Content = _webView;

        Loaded += async (_, _) => await InitializeAsync().ConfigureAwait(true);
        Closed += (_, _) => TryComplete(null);
    }

    public Task<string?> WaitForTokenAsync() => _tokenSource.Task;

    private async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        try
        {
            await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            TryComplete(null);
            return;
        }

        _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
        _webView.CoreWebView2.Navigate(_challengePageUri.AbsoluteUri);
    }

    private static bool IsAllowedChallengePageUri(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;
        if (uri.Scheme == Uri.UriSchemeHttp && IsLoopbackHost(uri.Host))
            return true;
        return false;
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

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
            return;
        if (!string.Equals(uri.Scheme, CallbackScheme, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        var token = ExtractToken(uri);
        if (!string.IsNullOrWhiteSpace(token))
            TryComplete(token);
    }

    private static string? ExtractToken(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query.TrimStart('?');
        var parts = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;
            if (!string.Equals(kv[0], "token", StringComparison.OrdinalIgnoreCase))
                continue;
            return WebUtility.UrlDecode(kv[1]);
        }

        return null;
    }

    private void TryComplete(string? token)
    {
        if (_completed)
            return;

        _completed = true;
        _tokenSource.TrySetResult(token);
        if (IsVisible)
            Close();
    }
}
