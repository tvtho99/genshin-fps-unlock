using System.IO;
using unlockfps_nc.Service;
using Xunit;

namespace unlockfps_nc.Tests;

public class ConfigServiceTests
{
    [Fact]
    public void CorruptJson_UsesDefaults_AndBacksUp()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "fps_config.json");
        File.WriteAllText(path, "{ not json");
        var svc = new ConfigService(path);
        Assert.Equal(120, svc.Config.FPSTarget);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void NullsAndOutOfRange_AreSanitized()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "fps_config.json");
        File.WriteAllText(path, """{"DllList":null,"GamePath":null,"AdditionalCommandLine":null,"FPSTarget":5}""");
        var c = new ConfigService(path).Config;
        Assert.NotNull(c.DllList);
        Assert.Equal("", c.GamePath);
        Assert.Equal("", c.AdditionalCommandLine);
        Assert.Equal(10, c.FPSTarget);
    }
}
