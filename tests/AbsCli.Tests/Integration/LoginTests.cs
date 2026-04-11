namespace AbsCli.Tests.Integration;

public class LoginTests : IntegrationTestBase
{
    [Fact(Skip = "Requires running ABS instance")]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        // This test requires interactive input — skip for now
        // Will be tested manually during development
    }
}
