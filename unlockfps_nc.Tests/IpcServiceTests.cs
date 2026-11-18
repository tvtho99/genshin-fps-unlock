using System.IO.MemoryMappedFiles;
using unlockfps_nc.Model;
using unlockfps_nc.Service;
using Xunit;

namespace unlockfps_nc.Tests;

public class IpcServiceTests
{
    [Fact]
    public void WriteSettings_PreservesStatus()
    {
        using var mmf = MemoryMappedFile.CreateNew(null, 4096);
        using var acc = mmf.CreateViewAccessor();
        acc.Write(IpcService.StatusOffset, (int)IpcStatus.Ready);
        IpcService.WriteSettings(acc, new Config { FPSTarget = 144, UsePowerSave = true, UseMobileUI = true });
        Assert.Equal(IpcStatus.Ready, IpcService.ReadStatus(acc));
        Assert.Equal(144, acc.ReadInt32(4));
        Assert.True(acc.ReadBoolean(8));
        Assert.True(acc.ReadBoolean(9));
    }
}
