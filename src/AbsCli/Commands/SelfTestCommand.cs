using System.CommandLine;
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Configuration;
using AbsCli.Models;
using AbsCli.Output;

namespace AbsCli.Commands;

public static class SelfTestCommand
{
    public static Command Create()
    {
        var command = new Command("self-test",
            "Verify AOT binary integrity — exercises all serialization paths without network access");

        command.SetHandler(() =>
        {
            var pass = 0;
            var fail = 0;

            void Check(string label, Action test)
            {
                try
                {
                    test();
                    Console.Error.WriteLine($"  PASS: {label}");
                    pass++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  FAIL: {label} — {ex.Message}");
                    fail++;
                }
            }

            Console.Error.WriteLine("=== JSON Serialization (source-generated) ===");

            Check("AppConfig round-trip", () =>
            {
                var config = new AppConfig
                {
                    Server = "https://test.example.com",
                    AccessToken = "token123",
                    RefreshToken = "refresh456",
                    DefaultLibrary = "lib-id"
                };
                var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig)!;
                Assert(back.Server == "https://test.example.com", "Server mismatch");
                Assert(back.AccessToken == "token123", "AccessToken mismatch");
                Assert(back.RefreshToken == "refresh456", "RefreshToken mismatch");
                Assert(back.DefaultLibrary == "lib-id", "DefaultLibrary mismatch");
            });

            Check("LoginRequest round-trip", () =>
            {
                var req = new LoginRequest { Username = "user", Password = "pass" };
                var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LoginRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LoginRequest)!;
                Assert(back.Username == "user", $"username: expected 'user', got '{back.Username}'");
                Assert(back.Password == "pass", $"password: expected 'pass', got '{back.Password}'");
            });

            Check("LoginResponse deserialize", () =>
            {
                var json = """
                {
                    "user": {
                        "id": "usr_1",
                        "username": "testuser",
                        "type": "admin",
                        "token": "tok",
                        "isActive": true,
                        "accessToken": "access",
                        "refreshToken": "refresh"
                    },
                    "userDefaultLibraryId": "lib_1",
                    "serverSettings": { "version": "2.33.1", "buildNumber": 1 }
                }
                """;
                var resp = JsonSerializer.Deserialize(json, AppJsonContext.Default.LoginResponse)!;
                Assert(resp.User.Id == "usr_1", $"user.id: expected 'usr_1', got '{resp.User.Id}'");
                Assert(resp.User.Username == "testuser", $"username: expected 'testuser', got '{resp.User.Username}'");
                Assert(resp.User.Type == "admin", $"type: expected 'admin', got '{resp.User.Type}'");
                Assert(resp.User.Token == "tok", $"token: expected 'tok', got '{resp.User.Token}'");
                Assert(resp.User.IsActive, "isActive should be true");
                Assert(resp.User.AccessToken == "access", $"accessToken: expected 'access', got '{resp.User.AccessToken}'");
                Assert(resp.User.RefreshToken == "refresh", $"refreshToken: expected 'refresh', got '{resp.User.RefreshToken}'");
                Assert(resp.UserDefaultLibraryId == "lib_1", $"defaultLibraryId: expected 'lib_1', got '{resp.UserDefaultLibraryId}'");
                Assert(resp.ServerSettings?.Version == "2.33.1", $"version: expected '2.33.1', got '{resp.ServerSettings?.Version}'");
                Assert(resp.ServerSettings?.BuildNumber == 1, $"buildNumber: expected 1, got {resp.ServerSettings?.BuildNumber}");
            });

            Check("Dictionary<string,string> round-trip", () =>
            {
                var dict = new Dictionary<string, string> { ["server"] = "https://test.com", ["lib"] = "abc" };
                var json = JsonSerializer.Serialize(dict, AppJsonContext.Default.DictionaryStringString);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString)!;
                Assert(back["server"] == "https://test.com", $"server: expected 'https://test.com', got '{back["server"]}'");
                Assert(back["lib"] == "abc", $"lib: expected 'abc', got '{back["lib"]}'");
            });

            Console.Error.WriteLine("");
            Console.Error.WriteLine("=== Configuration ===");

            Check("ConfigManager save/load round-trip", () =>
            {
                var tmp = Path.Combine(Path.GetTempPath(), $"abs-cli-selftest-{Guid.NewGuid()}", "config.json");
                var mgr = new ConfigManager(tmp);
                var config = new AppConfig
                {
                    Server = "https://roundtrip.test",
                    AccessToken = "at",
                    DefaultLibrary = "dl"
                };
                mgr.Save(config);
                var loaded = mgr.Load();
                Assert(loaded.Server == "https://roundtrip.test", "Server mismatch");
                Assert(loaded.AccessToken == "at", "AccessToken mismatch");
                Assert(loaded.DefaultLibrary == "dl", "DefaultLibrary mismatch");
                Directory.Delete(Path.GetDirectoryName(tmp)!, true);
            });

            Check("ConfigManager resolve precedence", () =>
            {
                var tmp = Path.Combine(Path.GetTempPath(), $"abs-cli-selftest-{Guid.NewGuid()}", "config.json");
                var mgr = new ConfigManager(tmp);
                mgr.Save(new AppConfig { Server = "https://file.test", AccessToken = "file-tok" });

                var resolved = mgr.Resolve(
                    flagServer: "https://flag.test",
                    flagToken: null,
                    envLookup: key => key == "ABS_TOKEN" ? "env-tok" : null);
                Assert(resolved.Server == "https://flag.test", "flag should win over file");
                Assert(resolved.AccessToken == "env-tok", "env should win over file");
                Directory.Delete(Path.GetDirectoryName(tmp)!, true);
            });

            Console.Error.WriteLine("");
            Console.Error.WriteLine("=== Filter Encoder ===");

            Check("Encode genre filter", () =>
            {
                var result = FilterEncoder.Encode("genres=Sci Fi");
                Assert(result == "genres.U2NpIEZp", $"expected genres.U2NpIEZp, got {result}");
            });

            Check("Pass-through already-encoded", () =>
            {
                var result = FilterEncoder.Encode("genres.U2NpIEZp");
                Assert(result == "genres.U2NpIEZp", "should pass through");
            });

            Check("Reject invalid format", () =>
            {
                var threw = false;
                try { FilterEncoder.Encode("invalid"); }
                catch (ArgumentException) { threw = true; }
                Assert(threw, "should throw ArgumentException");
            });

            Console.Error.WriteLine("");
            Console.Error.WriteLine("=== Token Helper ===");

            Check("Parse JWT expiration", () =>
            {
                // JWT with exp = 1775928439
                var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiJ0ZXN0IiwidHlwZSI6ImFjY2VzcyIsImlhdCI6MTc3NTkyNDgzOSwiZXhwIjoxNzc1OTI4NDM5fQ.fakesig";
                var exp = TokenHelper.GetExpiration(token);
                Assert(exp != null, "exp should not be null");
                Assert(exp!.Value.ToUnixTimeSeconds() == 1775928439, "wrong exp value");
            });

            Check("No exp returns null", () =>
            {
                var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiJ0ZXN0IiwiaWF0IjoxNzc1OTI0ODAyfQ.fakesig";
                Assert(TokenHelper.GetExpiration(token) == null, "should be null");
            });

            Check("Garbage input returns null", () =>
            {
                Assert(TokenHelper.GetExpiration("not-a-jwt") == null, "should be null");
            });

            Console.Error.WriteLine("");
            Console.Error.WriteLine("=== Console Output ===");

            Check("WriteJson produces valid JSON", () =>
            {
                var sw = new StringWriter();
                var original = Console.Out;
                Console.SetOut(sw);
                ConsoleOutput.WriteJson(new Dictionary<string, string> { ["test"] = "value" });
                Console.SetOut(original);
                var output = sw.ToString().Trim();
                Assert(output.Contains("\"test\""), "missing key in output");
                // Verify it's valid JSON
                JsonDocument.Parse(output);
            });

            Check("WriteRawJson passes through", () =>
            {
                var sw = new StringWriter();
                var original = Console.Out;
                Console.SetOut(sw);
                ConsoleOutput.WriteRawJson("{\"raw\":true}");
                Console.SetOut(original);
                Assert(sw.ToString().Trim() == "{\"raw\":true}", "raw passthrough failed");
            });

            // Summary
            Console.Error.WriteLine("");
            Console.Error.WriteLine($"========================================");
            Console.Error.WriteLine($"Results: {pass} passed, {fail} failed");
            Console.Error.WriteLine($"========================================");

            if (fail > 0)
                Environment.Exit(1);
        });

        return command;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
