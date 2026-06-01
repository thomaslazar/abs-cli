using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class LoginCommandTests
{
    [Fact]
    public void ReadPasswordFromStdin_TakesFirstLine_StripsTrailingNewline()
    {
        using var reader = new StringReader("hunter2\n");
        Assert.Equal("hunter2", LoginCommand.ReadPasswordFromStdin(reader));
    }

    [Fact]
    public void ReadPasswordFromStdin_HandlesCrLf()
    {
        using var reader = new StringReader("hunter2\r\n");
        Assert.Equal("hunter2", LoginCommand.ReadPasswordFromStdin(reader));
    }

    [Fact]
    public void ReadPasswordFromStdin_NoTrailingNewline()
    {
        using var reader = new StringReader("hunter2");
        Assert.Equal("hunter2", LoginCommand.ReadPasswordFromStdin(reader));
    }

    [Fact]
    public void ReadPasswordFromStdin_TakesOnlyFirstLine()
    {
        using var reader = new StringReader("hunter2\nignored\n");
        Assert.Equal("hunter2", LoginCommand.ReadPasswordFromStdin(reader));
    }

    [Fact]
    public void ReadPasswordFromStdin_EmptyReturnsEmpty()
    {
        using var reader = new StringReader("");
        Assert.Equal("", LoginCommand.ReadPasswordFromStdin(reader));
    }
}
