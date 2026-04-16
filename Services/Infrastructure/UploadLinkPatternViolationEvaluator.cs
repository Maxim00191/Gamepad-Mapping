#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Utils.Text;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class UploadLinkPatternViolationEvaluator : ITextContentViolationEvaluator
{
    public const string SuggestionResourceKey = "CommunityUpload_Suggest_LinkOrDomainNotAllowed";

    public IReadOnlyList<TextContentViolationMatch> Evaluate(IReadOnlyList<TextContentInspectionField> fields)
    {
        if (fields.Count == 0)
            return [];

        var results = new List<TextContentViolationMatch>();
        foreach (var field in fields)
        {
            if (!UploadFreeTextLinkDetector.ContainsBlockedContent(field.Value))
                continue;

            results.Add(new TextContentViolationMatch(
                field.ContextLabel,
                field.FieldCaption,
                UploadFreeTextLinkDetector.BlockedPatternId,
                SuggestionResourceKey));
        }

        return results;
    }
}
