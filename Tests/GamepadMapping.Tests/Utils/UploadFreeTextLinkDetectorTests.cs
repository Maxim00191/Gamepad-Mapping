using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadFreeTextLinkDetectorTests
{
    [Theory]
    [InlineData("see https://example.com/path")]
    [InlineData("see http://example.com")]
    [InlineData("prefix HXXPS://evil.example/test")]
    [InlineData("steam://run/12345")]
    [InlineData("visit www.example.com for info")]
    [InlineData("contact me@example.com please")]
    [InlineData("numeric 192.168.0.1 endpoint")]
    [InlineData("bare subdomain foo.bar.example.com here")]
    [InlineData("telegram t.me/somechannel")]
    [InlineData("shortener bit.ly/abc123")]
    [InlineData("Chinese short dwz.cn/xyz")]
    [InlineData("sale cheap.top/deal")]
    [InlineData("spam cheap.xyz/path")]
    [InlineData("phish prize.win")]
    [InlineData("fake shop.vip")]
    [InlineData("shady page.icu")]
    [InlineData("listing team.work")]
    [InlineData("get my.app/update")]
    [InlineData("free host bad.tk")]
    [InlineData("free host bad.ga")]
    [InlineData("tweet t.co/abc123")]
    [InlineData("short suo.im/1")]
    [InlineData("link url.cn/x")]
    [InlineData("cloud https://pan.baidu.com/s/1abc")]
    [InlineData("share pan.quark.cn/s/xyz")]
    public void ContainsBlockedContent_Positive(string text) =>
        Assert.True(UploadFreeTextLinkDetector.ContainsBlockedContent(text));

    [Theory]
    [InlineData("normal gameplay description")]
    [InlineData("version 1.2.3")]
    [InlineData("profile.json")]
    [InlineData("some.template.file")]
    [InlineData("cont.me")]
    [InlineData("")]
    public void ContainsBlockedContent_Negative(string text) =>
        Assert.False(UploadFreeTextLinkDetector.ContainsBlockedContent(text));
}
