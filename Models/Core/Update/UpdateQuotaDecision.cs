using System;

namespace GamepadMapperGUI.Models.Core;

public sealed record UpdateQuotaDecision(
    bool IsAllowed,
    UpdateQuotaAction Action,
    UpdateQuotaBlockReason BlockReason,
    int DailyLimit,
    int ConsumedToday,
    TimeSpan? RetryAfter)
{
    public int RemainingToday => Math.Max(0, DailyLimit - ConsumedToday);
}
