using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Storage;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityTemplateUploadComplianceService : ICommunityTemplateUploadComplianceService
{
    private static readonly ProfileValidator ProfileValidator = new();
    private readonly ITextContentViolationEvaluator _textPolicy;

    public CommunityTemplateUploadComplianceService(
        ITextContentViolationEvaluator? textContentViolationEvaluator = null)
    {
        _textPolicy = textContentViolationEvaluator ?? new CompositeTextContentViolationEvaluator(
            new UploadLinkPatternViolationEvaluator(),
            new UploadTextPolicyEvaluator());
    }

    public CommunityTemplateUploadComplianceResult EvaluateSubmission(
        IReadOnlyList<CommunityTemplateBundleEntry> selectedEntries,
        string gameFolderDisplayName,
        string authorDisplayName,
        string listingDescription)
    {
        var game = (gameFolderDisplayName ?? string.Empty).Trim();
        var author = (authorDisplayName ?? string.Empty).Trim();
        var desc = (listingDescription ?? string.Empty).Trim();

        string? catalogFolder = null;
        var step1Issues = new List<CommunityTemplateComplianceIssue>();

        if (game.Length == 0)
        {
            step1Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                "Game folder name is required.",
                "CommunityUpload_Suggest_GameFolder"));
        }

        if (author.Length == 0)
        {
            step1Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                "Author folder name is required.",
                "CommunityUpload_Suggest_AuthorFolder"));
        }

        if (desc.Length == 0)
        {
            step1Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                "Listing description is required.",
                "CommunityUpload_Suggest_ListingDescription"));
        }

        if (author.Length > 0
            && author.Length > CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength)
        {
            step1Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                string.Empty,
                "CommunityUpload_Suggest_AuthorNameRules",
                CommunityTemplateComplianceDetailKeys.AuthorNameTooLong,
                [CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength]));
        }
        else if (author.Length > 0
                 && !CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed(author))
        {
            step1Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                string.Empty,
                "CommunityUpload_Suggest_AuthorNameRules",
                CommunityTemplateComplianceDetailKeys.AuthorNameInvalidCharacters));
        }

        if (desc.Length > 0
            && desc.Length > CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters)
        {
            step1Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                string.Empty,
                "CommunityUpload_Suggest_ListingDescriptionLength",
                CommunityTemplateComplianceDetailKeys.ListingDescriptionTooLong,
                [CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters]));
        }

        if (step1Issues.Count == 0)
        {
            try
            {
                var gameSeg = TemplateStorageKey.ValidateSingleSegmentFolderForSave(game);
                var authorSeg = TemplateStorageKey.ValidateSingleSegmentFolderForSave(author);
                catalogFolder = TemplateStorageKey.ValidateCatalogFolderPathForSave($"{gameSeg}/{authorSeg}");
            }
            catch (ArgumentException ex)
            {
                step1Issues.Add(new CommunityTemplateComplianceIssue(string.Empty, ex.Message,
                    "CommunityUpload_Suggest_PathRules"));
            }
        }

        var step1Severity = step1Issues.Count > 0
            ? CommunityTemplateComplianceSeverity.Error
            : CommunityTemplateComplianceSeverity.Ok;

        var textPolicyIssues = new List<CommunityTemplateComplianceIssue>();
        var allTextFields = new List<TextContentInspectionField>();
        CommunityTemplateUploadFreeTextCollector.CollectSubmissionFields(game, author, desc, allTextFields);
        if (selectedEntries.Count > 0)
        {
            foreach (var entry in selectedEntries)
            {
                var label = FormatTemplateLabel(entry);
                var textModel = catalogFolder is not null
                    ? CommunityTemplateSubmissionClone.CloneForSubmission(
                        entry.Template,
                        catalogFolder,
                        author,
                        desc)
                    : entry.Template;
                CommunityTemplateUploadFreeTextCollector.CollectTemplateFields(textModel, label, allTextFields);
            }
        }

        foreach (var hit in _textPolicy.Evaluate(allTextFields))
        {
            var suggestionKey = string.IsNullOrEmpty(hit.SuggestionResourceKey)
                ? "CommunityUpload_Suggest_TextPolicyViolation"
                : hit.SuggestionResourceKey;
            var caption = hit.FieldCaption ?? string.Empty;
            var detail = $"{caption} contains text that is not allowed for community uploads.";
            textPolicyIssues.Add(new CommunityTemplateComplianceIssue(
                hit.ContextLabel,
                detail,
                suggestionKey,
                CommunityTemplateComplianceDetailKeys.TextPolicyFieldViolation,
                [caption]));
        }

        var textPolicySeverity = textPolicyIssues.Count > 0
            ? CommunityTemplateComplianceSeverity.Error
            : CommunityTemplateComplianceSeverity.Ok;

        var step2Issues = new List<CommunityTemplateComplianceIssue>();
        if (selectedEntries.Count == 0)
        {
            step2Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                "Select at least one template to upload.",
                "CommunityUpload_Suggest_SelectTemplate"));
        }
        else if (selectedEntries.Count > CommunityTemplateUploadConstraints.MaxFilesPerSubmission)
        {
            step2Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                $"Select at most {CommunityTemplateUploadConstraints.MaxFilesPerSubmission} templates per upload.",
                "CommunityUpload_Suggest_MaxFilesPerUpload"));
        }

        var profileIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in selectedEntries)
        {
            var pid = (e.Template.ProfileId ?? string.Empty).Trim();
            if (pid.Length == 0)
                continue;

            if (!profileIds.TryGetValue(pid, out var list))
            {
                list = [];
                profileIds[pid] = list;
            }

            list.Add(e.StorageKey);
        }

        foreach (var kv in profileIds)
        {
            if (kv.Value.Count <= 1)
                continue;

            var locs = string.Join(", ", kv.Value);
            step2Issues.Add(new CommunityTemplateComplianceIssue(
                string.Empty,
                $"Duplicate profileId '{kv.Key}' in this submission ({locs}).",
                "CommunityUpload_Suggest_DuplicateProfileId"));
        }

        var step2Severity = step2Issues.Count > 0
            ? CommunityTemplateComplianceSeverity.Error
            : CommunityTemplateComplianceSeverity.Ok;

        var step3Issues = new List<CommunityTemplateComplianceIssue>();
        var step3Warnings = new List<CommunityTemplateComplianceIssue>();

        if (step2Severity == CommunityTemplateComplianceSeverity.Ok && catalogFolder is not null)
        {
            foreach (var entry in selectedEntries)
            {
                var label = FormatTemplateLabel(entry);
                var clone = CommunityTemplateSubmissionClone.CloneForSubmission(
                    entry.Template,
                    catalogFolder,
                    author,
                    desc);
                var projectedRelativePath = $"{catalogFolder.Replace('\\', '/')}/{clone.ProfileId.Trim()}.json";
                if (!projectedRelativePath.EndsWith(
                        CommunityTemplateUploadConstraints.RequiredFileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    step3Issues.Add(new CommunityTemplateComplianceIssue(
                        label,
                        $"Only {CommunityTemplateUploadConstraints.RequiredFileExtension} files can be uploaded.",
                        "CommunityUpload_Suggest_JsonOnlyUpload"));
                    continue;
                }

                var projectedJson = JsonConvert.SerializeObject(clone, Newtonsoft.Json.Formatting.Indented);
                var projectedBytes = Encoding.UTF8.GetByteCount(projectedJson);
                if (projectedBytes > CommunityTemplateUploadConstraints.MaxTemplateFileBytes)
                {
                    step3Issues.Add(new CommunityTemplateComplianceIssue(
                        label,
                        $"Template exceeds the {CommunityTemplateUploadConstraints.MaxTemplateFileBytes} byte upload limit.",
                        "CommunityUpload_Suggest_TemplateFileSizeLimit"));
                    continue;
                }

                var v = ProfileValidator.Validate(clone);
                foreach (var err in v.Errors)
                {
                    step3Issues.Add(new CommunityTemplateComplianceIssue(
                        label,
                        err,
                        ResolveSuggestionKey(err)));
                }

                foreach (var resolverErr in TemplateKeyboardActionResolver.CollectResolutionErrors(clone))
                {
                    step3Issues.Add(new CommunityTemplateComplianceIssue(
                        label,
                        resolverErr,
                        ResolveSuggestionKey(resolverErr)));
                }

                foreach (var w in v.Warnings)
                {
                    step3Warnings.Add(new CommunityTemplateComplianceIssue(
                        label,
                        w,
                        "CommunityUpload_Suggest_WarningReview"));
                }
            }
        }

        var step3Severity = step3Issues.Count > 0
            ? CommunityTemplateComplianceSeverity.Error
            : step3Warnings.Count > 0
                ? CommunityTemplateComplianceSeverity.Warning
                : CommunityTemplateComplianceSeverity.Ok;

        var step3All = new List<CommunityTemplateComplianceIssue>();
        step3All.AddRange(step3Issues);
        step3All.AddRange(step3Warnings);

        var ready = step1Severity == CommunityTemplateComplianceSeverity.Ok
                    && textPolicySeverity == CommunityTemplateComplianceSeverity.Ok
                    && step2Severity == CommunityTemplateComplianceSeverity.Ok
                    && step3Severity != CommunityTemplateComplianceSeverity.Error;

        var steps = new[]
        {
            new CommunityTemplateComplianceStepResult(
                CommunityTemplateUploadComplianceStepKeys.SubmissionTitle,
                CommunityTemplateUploadComplianceStepKeys.SubmissionPrompt,
                step1Severity,
                step1Issues),
            new CommunityTemplateComplianceStepResult(
                CommunityTemplateUploadComplianceStepKeys.TextPolicyTitle,
                CommunityTemplateUploadComplianceStepKeys.TextPolicyPrompt,
                textPolicySeverity,
                textPolicyIssues),
            new CommunityTemplateComplianceStepResult(
                CommunityTemplateUploadComplianceStepKeys.SelectionTitle,
                CommunityTemplateUploadComplianceStepKeys.SelectionPrompt,
                step2Severity,
                step2Issues),
            new CommunityTemplateComplianceStepResult(
                CommunityTemplateUploadComplianceStepKeys.ContentTitle,
                CommunityTemplateUploadComplianceStepKeys.ContentPrompt,
                step3Severity,
                step3All)
        };

        return new CommunityTemplateUploadComplianceResult(ready, steps);
    }

    private static string FormatTemplateLabel(CommunityTemplateBundleEntry entry)
    {
        var dn = (entry.Template.DisplayName ?? string.Empty).Trim();
        var pid = (entry.Template.ProfileId ?? string.Empty).Trim();
        var head = dn.Length > 0 ? dn : (pid.Length > 0 ? pid : entry.StorageKey);
        return $"{head} ({entry.StorageKey})";
    }

    private static string? ResolveSuggestionKey(string message)
    {
        var m = message ?? string.Empty;
        if (m.Contains("Profile ID is required", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_ProfileId";
        if (m.Contains("'mappings' is required", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_MappingsRequired";
        if (m.Contains("Mapping has no input", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_MappingInput";
        if (m.Contains("has no output key", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_MappingOutput";
        if (m.Contains("keyboardActions", StringComparison.OrdinalIgnoreCase)
            && (m.Contains("unknown", StringComparison.OrdinalIgnoreCase)
                || m.Contains("missing or empty", StringComparison.OrdinalIgnoreCase)
                || m.Contains("Duplicate", StringComparison.Ordinal)))
            return "CommunityUpload_Suggest_KeyboardCatalog";
        if (m.Contains("templateCatalogFolder", StringComparison.OrdinalIgnoreCase)
            || m.Contains("Catalog folder", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_CatalogFolderJson";
        if (m.Contains("Radial Menu", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_RadialMenu";
        if (m.Contains("Template Group ID", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_TemplateGroupId";
        if (m.Contains("actionId cannot be used together", StringComparison.Ordinal))
            return "CommunityUpload_Suggest_ActionIdExclusive";
        return "CommunityUpload_Suggest_Generic";
    }
}
