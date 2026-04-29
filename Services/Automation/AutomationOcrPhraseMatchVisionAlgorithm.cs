#nullable enable

using System.Globalization;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationOcrPhraseMatchVisionAlgorithm : IAutomationVisionAlgorithm
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static OcrEngine? _engine;

    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.OcrPhraseMatch;

    public async ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = frame.ProbeOptions.EffectiveOcrPhraseMatchOptions;
        var phrases = ParsePhrases(opts.PhrasesMultiline);
        if (phrases.Count == 0)
            return new AutomationVisionResult(false, 0, 0);

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var engine = _engine ??= CreateEngine();
            if (engine is null)
                return new AutomationVisionResult(false, 0, 0);

            var maxEdge = AutomationOcrBitmapEncoding.ResolveMaxLongEdge(opts.MaxLongEdgePx);
            using var softwareBitmap = AutomationOcrBitmapEncoding.ToSoftwareBitmap(frame.Image, maxEdge);
            var ocrResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);
            var fullText = string.Join(" ", ocrResult.Lines.Select(l => l.Text));
            if (string.IsNullOrWhiteSpace(fullText))
                return new AutomationVisionResult(false, 0, 0);

            var haystack = NormalizeWhitespace(fullText);
            foreach (var phrase in phrases)
            {
                if (PhraseContained(haystack, phrase, opts.CaseSensitive))
                {
                    var confidence = Math.Clamp(fullText.Length / 4000d, 0.05d, 1d);
                    return new AutomationVisionResult(true, 0, 0, 1, confidence);
                }
            }

            return new AutomationVisionResult(false, 0, 0);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static OcrEngine? CreateEngine() =>
        OcrEngine.TryCreateFromUserProfileLanguages()
        ?? OcrEngine.TryCreateFromLanguage(new Language(CultureInfo.CurrentUICulture.Name));

    private static List<string> ParsePhrases(string multiline)
    {
        if (string.IsNullOrWhiteSpace(multiline))
            return [];

        return multiline
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static string NormalizeWhitespace(string text)
    {
        var span = text.AsSpan();
        var sb = new System.Text.StringBuilder(span.Length);
        var lastWasSpace = true;
        foreach (var c in span)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    private static bool PhraseContained(string normalizedHaystack, string phrase, bool caseSensitive)
    {
        var p = NormalizeWhitespace(phrase);
        if (p.Length == 0)
            return false;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return normalizedHaystack.Contains(p, comparison);
    }
}
