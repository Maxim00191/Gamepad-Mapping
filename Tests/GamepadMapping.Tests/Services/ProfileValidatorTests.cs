using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Storage;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class ProfileValidatorTests
{
    [Fact]
    public void Validate_NullMappings_IsError()
    {
        var v = new ProfileValidator();
        var r = v.Validate(new GameProfileTemplate { ProfileId = "x", Mappings = null! });
        Assert.Contains(r.Errors, e => e.Contains("'mappings' is required", StringComparison.Ordinal));
    }
}
