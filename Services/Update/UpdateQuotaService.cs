using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Update;

public sealed class UpdateQuotaService : IUpdateQuotaService
{
    private const int MinCheckCooldownSeconds = 10;
    private const int MaxCheckCooldownSeconds = 600;
    private const int MinCheckDailyLimit = 5;
    private const int MaxCheckDailyLimit = 200;
    private const int MinDownloadDailyLimit = 1;
    private const int MaxDownloadDailyLimit = 50;

    private readonly object _syncRoot = new();
    private readonly IUpdateQuotaPolicyProvider _policyProvider;
    private readonly string _stateFilePath;
    private readonly ITrustedUtcTimeService _trustedUtcTimeService;

    public UpdateQuotaService(
        IUpdateQuotaPolicyProvider? policyProvider = null,
        ITrustedUtcTimeService? trustedUtcTimeService = null)
    {
        _policyProvider = policyProvider ?? new StaticUpdateQuotaPolicyProvider();
        _stateFilePath = AppPaths.GetUpdateQuotaStateFilePath();
        _trustedUtcTimeService = trustedUtcTimeService ?? new TrustedUtcTimeService();
    }

    public async Task<UpdateQuotaDecision> TryConsumeQuotaAsync(UpdateQuotaAction action)
    {
        var now = await _trustedUtcTimeService.GetUtcNowAsync(CancellationToken.None);
        lock (_syncRoot)
        {
            var state = LoadStateOrDefault();
            now = EnforceMonotonicTime(state, now);
            RotateIfNewDay(state, now);

            var decision = action switch
            {
                UpdateQuotaAction.Check => TryConsumeCheckQuota(state, now),
                UpdateQuotaAction.Download => TryConsumeDownloadQuota(state),
                _ => new UpdateQuotaDecision(false, action, UpdateQuotaBlockReason.DailyLimit, 0, 0, null)
            };

            if (decision.IsAllowed)
            {
                state.LastObservedUnixSeconds = now.ToUnixTimeSeconds();
                SaveState(state);
            }

            return decision;
        }
    }

    private static DateTimeOffset EnforceMonotonicTime(UpdateQuotaState state, DateTimeOffset now)
    {
        var lastObserved = state.LastObservedUnixSeconds;
        if (lastObserved <= 0)
            return now;

        var nowUnix = now.ToUnixTimeSeconds();
        if (nowUnix >= lastObserved)
            return now;

        return DateTimeOffset.FromUnixTimeSeconds(lastObserved);
    }

    private UpdateQuotaDecision TryConsumeCheckQuota(UpdateQuotaState state, DateTimeOffset now)
    {
        var policy = _policyProvider.GetCurrentPolicy();
        var checkDailyLimit = Clamp(policy.CheckDailyLimit, MinCheckDailyLimit, MaxCheckDailyLimit);
        if (state.CheckCount >= checkDailyLimit)
        {
            return new UpdateQuotaDecision(
                false,
                UpdateQuotaAction.Check,
                UpdateQuotaBlockReason.DailyLimit,
                checkDailyLimit,
                state.CheckCount,
                null);
        }

        var cooldownSeconds = Clamp(policy.CheckCooldownSeconds, MinCheckCooldownSeconds, MaxCheckCooldownSeconds);
        var lastCheckAt = DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, state.LastCheckUnixSeconds));
        var elapsed = now - lastCheckAt;
        if (state.LastCheckUnixSeconds > 0 && elapsed < TimeSpan.FromSeconds(cooldownSeconds))
        {
            return new UpdateQuotaDecision(
                false,
                UpdateQuotaAction.Check,
                UpdateQuotaBlockReason.Cooldown,
                checkDailyLimit,
                state.CheckCount,
                TimeSpan.FromSeconds(cooldownSeconds) - elapsed);
        }

        state.CheckCount += 1;
        state.LastCheckUnixSeconds = now.ToUnixTimeSeconds();

        return new UpdateQuotaDecision(
            true,
            UpdateQuotaAction.Check,
            UpdateQuotaBlockReason.None,
            checkDailyLimit,
            state.CheckCount,
            null);
    }

    private UpdateQuotaDecision TryConsumeDownloadQuota(UpdateQuotaState state)
    {
        var policy = _policyProvider.GetCurrentPolicy();
        var downloadDailyLimit = Clamp(policy.DownloadDailyLimit, MinDownloadDailyLimit, MaxDownloadDailyLimit);
        if (state.DownloadCount >= downloadDailyLimit)
        {
            return new UpdateQuotaDecision(
                false,
                UpdateQuotaAction.Download,
                UpdateQuotaBlockReason.DailyLimit,
                downloadDailyLimit,
                state.DownloadCount,
                null);
        }

        state.DownloadCount += 1;
        return new UpdateQuotaDecision(
            true,
            UpdateQuotaAction.Download,
            UpdateQuotaBlockReason.None,
            downloadDailyLimit,
            state.DownloadCount,
            null);
    }

    private static void RotateIfNewDay(UpdateQuotaState state, DateTimeOffset now)
    {
        var nowKey = now.UtcDateTime.ToString("yyyyMMdd");
        if (string.Equals(state.UtcDateKey, nowKey, StringComparison.Ordinal))
            return;

        state.UtcDateKey = nowKey;
        state.CheckCount = 0;
        state.DownloadCount = 0;
        state.LastCheckUnixSeconds = 0;
    }

    private UpdateQuotaState LoadStateOrDefault()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
                return CreateDefaultState();

            var encrypted = File.ReadAllBytes(_stateFilePath);
            if (encrypted.Length == 0)
                return CreateDefaultState();

            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plain);
            var state = JsonSerializer.Deserialize<UpdateQuotaState>(json);
            return state ?? CreateDefaultState();
        }
        catch
        {
            return CreateDefaultState();
        }
    }

    private void SaveState(UpdateQuotaState state)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(state);
        var plain = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_stateFilePath, encrypted);
    }

    private static UpdateQuotaState CreateDefaultState()
    {
        var now = DateTimeOffset.UtcNow;
        return new UpdateQuotaState
        {
            UtcDateKey = now.UtcDateTime.ToString("yyyyMMdd")
        };
    }

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}


