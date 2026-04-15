#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class CommunityTemplateWorkerSubmissionAck
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("pullRequestHtmlUrl")]
    public string? PullRequestHtmlUrl { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("github")]
    public CommunityTemplateWorkerGithubApiErrorPayload? GitHub { get; init; }

    public string BuildFailureMessage(int workerHttpStatusCode)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(Error))
            lines.Add(Error.Trim());

        if (!string.IsNullOrWhiteSpace(Detail))
            lines.Add(Detail.Trim());

        if (!string.IsNullOrWhiteSpace(Phase))
            lines.Add(string.Format(CultureInfo.CurrentCulture, "Phase: {0}", Phase.Trim()));

        if (GitHub is not null)
        {
            if (GitHub.Status is int gh)
                lines.Add(string.Format(CultureInfo.CurrentCulture, "GitHub HTTP: {0}", gh));

            if (!string.IsNullOrWhiteSpace(GitHub.Message))
                lines.Add(string.Format(CultureInfo.CurrentCulture, "GitHub message: {0}", GitHub.Message.Trim()));

            if (GitHub.Errors.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "GitHub errors: {0}",
                    GitHub.Errors.GetRawText()));
            }
        }

        lines.Add(string.Format(CultureInfo.CurrentCulture, "Worker HTTP: {0}", workerHttpStatusCode));

        if (!string.IsNullOrWhiteSpace(RequestId))
            lines.Add(string.Format(CultureInfo.CurrentCulture, "Request ID: {0}", RequestId.Trim()));

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildUnparsedFailureMessage(int workerHttpStatusCode, string? responseBody)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.CurrentCulture, $"Worker HTTP: {workerHttpStatusCode}");
        var raw = (responseBody ?? string.Empty).Trim();
        if (raw.Length > 0)
        {
            sb.AppendLine();
            sb.Append(raw.Length > 4000 ? string.Concat(raw.AsSpan(0, 4000), "…") : raw);
        }

        return sb.ToString();
    }
}
