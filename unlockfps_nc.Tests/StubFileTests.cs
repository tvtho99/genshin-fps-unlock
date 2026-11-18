using unlockfps_nc.Service;
using unlockfps_nc.Utility;
using Xunit;

namespace unlockfps_nc.Tests;

public class StubFileTests
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [Fact]
    public void Extract_WritesContentAndLocksFile()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;

        using (var stub = StubFile.Extract(dir, Content))
        {
            Assert.Equal(Path.Combine(dir, StubFile.FileName), stub.Path);
            Assert.Equal(Content, File.ReadAllBytes(stub.Path));
            Assert.Throws<IOException>(() => File.OpenWrite(stub.Path).Dispose());
            Assert.Throws<IOException>(() => File.Delete(stub.Path));
        }

        // Released after Dispose
        File.Delete(Path.Combine(dir, StubFile.FileName));
    }

    [Fact]
    public void Extract_OverwritesStaleFile()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllBytes(Path.Combine(dir, StubFile.FileName), [9, 9]);

        using var stub = StubFile.Extract(dir, Content);

        Assert.Equal(Content, File.ReadAllBytes(stub.Path));
    }

    [Fact]
    public void Extract_ThrowsWhenAnotherProcessHoldsTheFile()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, StubFile.FileName);
        File.WriteAllBytes(path, [9]);

        using var hostile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.ThrowsAny<IOException>(() => StubFile.Extract(dir, Content));
    }

    [Fact]
    public void ExtractEmbedded_LockedStubCanStillBeLoaded()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;

        using var stub = StubFile.ExtractEmbedded(dir);

        // The loader must be able to map the file while we hold it locked against writers.
        var module = Native.LoadLibrary(stub.Path);
        Assert.NotEqual(IntPtr.Zero, module);
        Assert.NotEqual(IntPtr.Zero, Native.GetProcAddress(module, "WndProc"));
    }
}
