using Xunit;

namespace unlockfps_nc.Tests;

public class SmokeTests
{
    [Fact]
    public void Version_IsPositive() => Assert.True(Program.Version > 0);
}
