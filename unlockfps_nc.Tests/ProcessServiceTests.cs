using unlockfps_nc.Model;
using unlockfps_nc.Service;
using Xunit;

namespace unlockfps_nc.Tests;

public class ProcessServiceTests
{
    [Fact]
    public void BuildCommandLine_QuotesPathWithSpaces()
    {
        var cfg = new Config { GamePath = @"C:\Program Files\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe" };
        var cmd = ProcessService.BuildCommandLine(cfg);
        Assert.StartsWith("\"C:\\Program Files\\HoYoPlay\\games\\Genshin Impact game\\GenshinImpact.exe\" ", cmd);
        Assert.Contains("-screen-fullscreen 1 -window-mode borderless", cmd);
    }

    [Fact]
    public void BuildCommandLine_CustomRes()
        => Assert.Contains("-screen-width 2560 -screen-height 1440",
            ProcessService.BuildCommandLine(new Config { GamePath = "a.exe", UseCustomRes = true, CustomResX = 2560, CustomResY = 1440 }));

    [Theory, InlineData(0, 0x100u), InlineData(3, 0x20u), InlineData(5, 0x40u)]
    public void PriorityClassFor_MapsComboIndex(int idx, uint expected) => Assert.Equal(expected, ProcessService.PriorityClassFor(idx));
}
