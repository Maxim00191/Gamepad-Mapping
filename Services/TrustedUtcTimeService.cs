using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapperGUI.Services;

public sealed class TrustedUtcTimeService : ITrustedUtcTimeService
{
    private static readonly Uri[] TimeProbeEndpoints =
    [
        new("https://api.github.com/rate_limit"),
        new("https://www.microsoft.com")
    ];

    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    private const string UserAgent = "GamepadMapping/1.0 (trusted-time)";

    private readonly object _syncRoot = new();
    private readonly HttpClient _httpClient;

    private DateTimeOffset? _lastTrustedUtc;
    private long _lastTrustedTimestamp;
    private DateTimeOffset _lastAttemptUtc = DateTimeOffset.MinValue;

    public TrustedUtcTimeService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<DateTimeOffset> GetUtcNowAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldAttemptSync())
            await TrySyncTrustedTimeAsync(cancellationToken);

        lock (_syncRoot)
        {
            if (_lastTrustedUtc is null)
                return DateTimeOffset.UtcNow;

            var elapsed = Stopwatch.GetElapsedTime(_lastTrustedTimestamp);
            return _lastTrustedUtc.Value + elapsed;
        }
    }

    private bool ShouldAttemptSync()
    {
        lock (_syncRoot)
        {
            if (_lastTrustedUtc is null)
                return true;

            var elapsedSinceAttempt = DateTimeOffset.UtcNow - _lastAttemptUtc;
            return elapsedSinceAttempt >= SyncInterval;
        }
    }

    private async Task TrySyncTrustedTimeAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _lastAttemptUtc = DateTimeOffset.UtcNow;
        }

        foreach (var endpoint in TimeProbeEndpoints)
        {
            var trusted = await TryGetDateHeaderUtcAsync(endpoint, cancellationToken);
            if (!trusted.HasValue)
                continue;

            lock (_syncRoot)
            {
                _lastTrustedUtc = trusted.Value;
                _lastTrustedTimestamp = Stopwatch.GetTimestamp();
            }
            return;
        }
    }

    private async Task<DateTimeOffset?> TryGetDateHeaderUtcAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(RequestTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            if (!response.Headers.Date.HasValue)
                return null;

            return response.Headers.Date.Value.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }
}
