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
    [InlineData("bypass 123bit.ly/abc123")]
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
    [InlineData("fullwidth scheme ｈｔｔｐｓ：／／ｅｘａｍｐｌｅ．ｃｏｍ／a")]
    [InlineData("accented bypass h\u00E8tps://ex\u00E1mple.com/path")]
    [InlineData("mixed width + accent ｈt\u00E9p\uFF53://\uFF57\uFF57\uFF57.\u00E9xample.com")]
    [InlineData("obfuscated ip 132.155.xxx.xxx")]
    [InlineData("ipv6 bracket [2001:0db8:85a3::8a2e:0370:7334]")]
    [InlineData("ipv6 bare 2001:db8::1")]
    public void ContainsBlockedContent_Positive(string text) =>
        Assert.True(UploadFreeTextLinkDetector.ContainsBlockedContent(text));

    [Theory]
    [InlineData("normal gameplay description")]
    [InlineData("version 1.2.3")]
    [InlineData("profile.json")]
    [InlineData("some.template.file")]
    [InlineData("cont.me")]
    [InlineData("ability")]
    [InlineData("clock 12:34:56")]
    [InlineData("")]
    public void ContainsBlockedContent_Negative(string text) =>
        Assert.False(UploadFreeTextLinkDetector.ContainsBlockedContent(text));
}
