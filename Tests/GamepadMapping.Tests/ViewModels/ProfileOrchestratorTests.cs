using System.Collections.ObjectModel;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using Moq;

namespace GamepadMapping.Tests.ViewModels;

public sealed class ProfileOrchestratorTests
{
    [Fact]
    public void LoadSelectedTemplate_UsesCanonicalDisplayName_NotLocalizedProjection()
    {
        var profileService = new Mock<IProfileService>();
        var processTargetService = new Mock<IProcessTargetService>();

        var selected = new TemplateOption
        {
            ProfileId = "explore-maxim0191",
            TemplateGroupId = "roco-kingdom",
            DisplayNameBaseline = "Roco Kingdom Explore",
            DisplayNames = new Dictionary<string, string> { ["zh-CN"] = "洛克王国探索" }
        };

        profileService.SetupGet(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption> { selected });
        profileService.SetupGet(p => p.LastSelectedTemplateProfileId).Returns(selected.StorageKey);
        profileService.Setup(p => p.ReloadTemplates(selected.StorageKey)).Returns(selected);
        profileService.Setup(p => p.LoadSelectedTemplate(selected)).Returns(new GameProfileTemplate
        {
            ProfileId = selected.ProfileId,
            TemplateGroupId = selected.TemplateGroupId,
            DisplayName = "Roco Kingdom Explore",
            DisplayNames = new Dictionary<string, string> { ["zh-CN"] = "洛克王国探索" },
            Mappings = []
        });
        profileService.Setup(p => p.SaveTemplate(It.IsAny<GameProfileTemplate>(), It.IsAny<bool>()));

        var orchestrator = new ProfileOrchestrator(profileService.Object, processTargetService.Object);

        Assert.Equal("Roco Kingdom Explore", orchestrator.CurrentTemplateDisplayName);
    }

    [Fact]
    public void RefreshCurrentIdentityDisplayNameForCulture_DoesNotMutateEditableDisplayName()
    {
        var profileService = new Mock<IProfileService>();
        var processTargetService = new Mock<IProcessTargetService>();

        var selected = new TemplateOption
        {
            ProfileId = "fight-maxim0191",
            TemplateGroupId = "roco-kingdom",
            DisplayNameBaseline = "Roco Kingdom Fight",
            DisplayNames = new Dictionary<string, string> { ["zh-CN"] = "洛克王国战斗" }
        };

        profileService.SetupGet(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption> { selected });
        profileService.SetupGet(p => p.LastSelectedTemplateProfileId).Returns(selected.StorageKey);
        profileService.Setup(p => p.ReloadTemplates(selected.StorageKey)).Returns(selected);
        profileService.Setup(p => p.LoadSelectedTemplate(selected)).Returns(new GameProfileTemplate
        {
            ProfileId = selected.ProfileId,
            TemplateGroupId = selected.TemplateGroupId,
            DisplayName = "Roco Kingdom Fight",
            DisplayNames = new Dictionary<string, string> { ["zh-CN"] = "洛克王国战斗" },
            Mappings = []
        });
        profileService.Setup(p => p.SaveTemplate(It.IsAny<GameProfileTemplate>(), It.IsAny<bool>()));

        var orchestrator = new ProfileOrchestrator(profileService.Object, processTargetService.Object);

        orchestrator.CurrentTemplateDisplayName = "Custom Edited Name";
        orchestrator.RefreshCurrentIdentityDisplayNameForCulture(new TranslationService());

        Assert.Equal("Custom Edited Name", orchestrator.CurrentTemplateDisplayName);
    }
}
