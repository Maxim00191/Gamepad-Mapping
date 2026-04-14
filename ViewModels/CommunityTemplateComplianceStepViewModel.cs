using System.Collections.ObjectModel;
using GamepadMapperGUI.Models.Core.Community;

namespace Gamepad_Mapping.ViewModels;

public sealed class CommunityTemplateComplianceStepViewModel
{
    public CommunityTemplateComplianceStepViewModel(
        string title,
        string prompt,
        string statusSummary,
        CommunityTemplateComplianceSeverity severity,
        ObservableCollection<CommunityTemplateComplianceIssueViewModel> items)
    {
        Title = title;
        Prompt = prompt;
        StatusSummary = statusSummary;
        Severity = severity;
        Items = items;
    }

    public string Title { get; }

    public string Prompt { get; }

    public string StatusSummary { get; }

    public CommunityTemplateComplianceSeverity Severity { get; }

    public ObservableCollection<CommunityTemplateComplianceIssueViewModel> Items { get; }
}
