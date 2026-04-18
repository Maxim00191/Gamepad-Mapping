using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.UploadTextPolicy;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyEmbeddedReaderTests
{
    [Fact]
    public void TryReadPlaintextPolicyUtf8_DecompressesAndParses()
    {
        Assert.True(UploadTextPolicyEmbeddedReader.TryReadPlaintextPolicyUtf8(out var text));
        Assert.False(string.IsNullOrEmpty(text));

        var rows = UploadTextPolicyTextParser.Parse(text!);
        Assert.NotEmpty(rows);
    }
}
