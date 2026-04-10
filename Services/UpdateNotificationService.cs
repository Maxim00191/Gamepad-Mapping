using System.IO;
using System.Text;
using System.Text.Json;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services;

public sealed class UpdateNotificationService : IUpdateNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UpdateSuccessToastPending? TryGetPendingSuccessToast()
    {
        try
        {
            var securityPath = AppPaths.GetUpdateSecurityStateFilePath();
            if (!File.Exists(securityPath))
                return null;

            var json = File.ReadAllText(securityPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var state = JsonSerializer.Deserialize<UpdateSecurityState>(json);
            if (state is null || string.IsNullOrWhiteSpace(state.HighestTrustedReleaseTag))
                return null;

            var updatedAt = state.UpdatedAtUnixSeconds;
            if (updatedAt <= 0)
                return null;

            var ackPath = AppPaths.GetUpdateSuccessToastAckFilePath();
            if (File.Exists(ackPath))
            {
                try
                {
                    var ackJson = File.ReadAllText(ackPath, Encoding.UTF8);
                    var ack = JsonSerializer.Deserialize<UpdateSuccessToastAckState>(ackJson);
                    if (ack is not null && ack.AcknowledgedUpdatedAtUnixSeconds >= updatedAt)
                        return null;
                }
                catch
                {
                    // Treat invalid ack as missing so the user still sees the notice once.
                }
            }

            return new UpdateSuccessToastPending(state.HighestTrustedReleaseTag.Trim(), updatedAt);
        }
        catch
        {
            return null;
        }
    }

    public UpdateFailureToastPending? TryGetPendingFailureToast()
    {
        try
        {
            var resultPath = AppPaths.GetUpdateLastResultFilePath();
            if (!File.Exists(resultPath))
                return null;

            var json = File.ReadAllText(resultPath, Encoding.UTF8);
            var result = JsonSerializer.Deserialize<UpdateResultInfo>(json);

            if (result?.Status == "failed")
            {
                return new UpdateFailureToastPending(result.Message, result.TimestampUnixSeconds);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void AcknowledgeSuccess(long updatedAtUnixSeconds)
    {
        try
        {
            var ackPath = AppPaths.GetUpdateSuccessToastAckFilePath();
            var dir = Path.GetDirectoryName(ackPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var payload = new UpdateSuccessToastAckState { AcknowledgedUpdatedAtUnixSeconds = updatedAtUnixSeconds };
            File.WriteAllText(ackPath, JsonSerializer.Serialize(payload, JsonOptions), new UTF8Encoding(false));

            // Also clean up the result file if it exists and was a success
            AcknowledgeResultFileIfMatches("success");
        }
        catch
        {
            // Best-effort
        }
    }

    public void AcknowledgeFailure()
    {
        AcknowledgeResultFileIfMatches("failed");
    }

    private void AcknowledgeResultFileIfMatches(string status)
    {
        try
        {
            var resultPath = AppPaths.GetUpdateLastResultFilePath();
            if (File.Exists(resultPath))
            {
                var json = File.ReadAllText(resultPath, Encoding.UTF8);
                var result = JsonSerializer.Deserialize<UpdateResultInfo>(json);
                if (result?.Status == status)
                {
                    File.Delete(resultPath);
                }
            }
        }
        catch
        {
            // Best-effort
        }
    }

    private class UpdateResultInfo
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public long TimestampUnixSeconds { get; set; }
    }
}
