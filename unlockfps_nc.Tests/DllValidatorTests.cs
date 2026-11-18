using unlockfps_nc.Utility;
using Xunit;

namespace unlockfps_nc.Tests;

public class DllValidatorTests
{
    [Fact]
    public void Missing_False() => Assert.False(DllValidator.IsNativeX64Dll(@"C:\nope\x.dll"));

    [Fact]
    public void TextFile_False()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "hi");
            Assert.False(DllValidator.IsNativeX64Dll(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ManagedAssembly_False() => Assert.False(DllValidator.IsNativeX64Dll(typeof(DllValidatorTests).Assembly.Location));

    [Fact]
    public void Kernel32_True() => Assert.True(DllValidator.IsNativeX64Dll(Path.Combine(Environment.SystemDirectory, "kernel32.dll")));
}
