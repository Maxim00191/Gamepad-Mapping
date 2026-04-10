using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GamepadMapping.Tests.Services;

public class TemplateKeyboardActionResolverTests
{
    [Fact]
    public void Apply_ResolvesActionId_ToKeyboardKey()
    {
        var template = new GameProfileTemplate
        {
            KeyboardActions =
            [
                new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space", Description = "Jump" }
            ],
            Mappings =
            [
                new MappingEntry
                {
                    ActionId = "jump",
                    From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                    Trigger = TriggerMoment.Pressed
                }
            ]
        };

        TemplateKeyboardActionResolver.Apply(template);

        Assert.Equal("Space", template.Mappings[0].KeyboardKey);
        Assert.Equal("Jump", template.Mappings[0].Description);
    }

    [Fact]
    public void Apply_LegacyMapping_WithoutCatalog_Unchanged()
    {
        var template = new GameProfileTemplate
        {
            Mappings =
            [
                new MappingEntry
                {
                    KeyboardKey = "F",
                    From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Y" },
                    Trigger = TriggerMoment.Pressed
                }
            ]
        };

        TemplateKeyboardActionResolver.Apply(template);

        Assert.Equal("F", template.Mappings[0].KeyboardKey);
    }

    [Fact]
    public void Apply_DuplicateCatalogId_Throws()
    {
        var template = new GameProfileTemplate
        {
            KeyboardActions =
            [
                new KeyboardActionDefinition { Id = "a", KeyboardKey = "A" },
                new KeyboardActionDefinition { Id = "a", KeyboardKey = "B" }
            ],
            Mappings = []
        };

        Assert.Throws<InvalidOperationException>(() => TemplateKeyboardActionResolver.Apply(template));
    }

    [Fact]
    public void Apply_UnknownActionId_Throws()
    {
        var template = new GameProfileTemplate
        {
            KeyboardActions = [new KeyboardActionDefinition { Id = "only", KeyboardKey = "X" }],
            Mappings =
            [
                new MappingEntry { ActionId = "missing", From = new GamepadBinding(), Trigger = TriggerMoment.Pressed }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => TemplateKeyboardActionResolver.Apply(template));
    }

    [Fact]
    public void Serialize_WithActionId_OmitsPrimaryKeyboardKeyProperty()
    {
        var entry = new MappingEntry
        {
            ActionId = "roll",
            KeyboardKey = "LeftCtrl",
            From = new GamepadBinding(),
            Trigger = TriggerMoment.Pressed
        };

        var json = JsonConvert.SerializeObject(entry);
        var jo = JObject.Parse(json);
        Assert.Null(jo["keyboardKey"]);
        Assert.Equal("roll", (string?)jo["actionId"]);
    }
}

