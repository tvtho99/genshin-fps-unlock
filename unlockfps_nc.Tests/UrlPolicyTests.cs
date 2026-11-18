using unlockfps_nc.Utility;
using Xunit;

namespace unlockfps_nc.Tests;

public class UrlPolicyTests
{
    [Theory]
    [InlineData("https://github.com/34736384/genshin-fps-unlock/releases", true)]
    [InlineData("https://GitHub.com/34736384/genshin-fps-unlock/releases", true)]
    [InlineData("http://github.com/x", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    [InlineData("https://evil.example/github.com", false)]
    [InlineData("https://user@evil.example", false)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData(@"C:\Windows\System32\cmd.exe", false)]
    [InlineData(@"\\server\share\setup.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTrustedReleaseUrl(string? url, bool expected)
        => Assert.Equal(expected, UrlPolicy.IsTrustedReleaseUrl(url));
}
