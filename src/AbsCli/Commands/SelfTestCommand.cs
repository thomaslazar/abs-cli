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
            Console.Error.WriteLine("=== API Response DTOs (source-generated) ===");

            Check("LibraryListResponse round-trip", () =>
            {
                var obj = new LibraryListResponse
                {
                    Libraries = new List<Library>
                    {
                        new() { Id = "lib_1", Name = "Test", MediaType = "book" }
                    }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.LibraryListResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryListResponse)!;
                Assert(back.Libraries.Count == 1, $"expected 1 library, got {back.Libraries.Count}");
                Assert(back.Libraries[0].Id == "lib_1", $"id mismatch: {back.Libraries[0].Id}");
                Assert(back.Libraries[0].Name == "Test", $"name mismatch: {back.Libraries[0].Name}");
            });

            Check("Library round-trip", () =>
            {
                var obj = new Library { Id = "lib_2", Name = "Audio", MediaType = "book", DisplayOrder = 1 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Library);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Library)!;
                Assert(back.Id == "lib_2", $"id: {back.Id}");
                Assert(back.DisplayOrder == 1, $"displayOrder: {back.DisplayOrder}");
            });

            Check("PaginatedResponse round-trip", () =>
            {
                var json = """{"results":[{"id":"item1"}],"total":42,"limit":10,"page":0}""";
                var obj = JsonSerializer.Deserialize(json, AppJsonContext.Default.PaginatedResponse)!;
                Assert(obj.Total == 42, $"total: {obj.Total}");
                Assert(obj.Limit == 10, $"limit: {obj.Limit}");
                Assert(obj.Results.Count == 1, $"results count: {obj.Results.Count}");
                var roundTrip = JsonSerializer.Serialize(obj, AppJsonContext.Default.PaginatedResponse);
                Assert(roundTrip.Contains("\"total\":42") || roundTrip.Contains("\"total\": 42"), "total not in output");
            });

            Check("LibraryItemMinified round-trip", () =>
            {
                var obj = new LibraryItemMinified { Id = "li_1", LibraryId = "lib_1", MediaType = "book" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.LibraryItemMinified);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryItemMinified)!;
                Assert(back.Id == "li_1", $"id: {back.Id}");
                Assert(back.MediaType == "book", $"mediaType: {back.MediaType}");
            });

            Check("SearchResult round-trip", () =>
            {
                var json = """{"book":[],"narrators":[],"tags":[],"genres":[],"series":[],"authors":[]}""";
                var obj = JsonSerializer.Deserialize(json, AppJsonContext.Default.SearchResult)!;
                Assert(obj.Book != null, "book null");
                Assert(obj.Authors != null, "authors null");
                var roundTrip = JsonSerializer.Serialize(obj, AppJsonContext.Default.SearchResult);
                Assert(roundTrip.Contains("\"book\""), "book key missing in output");
            });

            Check("UpdateMediaResponse round-trip", () =>
            {
                var json = """{"updated":true,"libraryItem":{"id":"li_1"}}""";
                var obj = JsonSerializer.Deserialize(json, AppJsonContext.Default.UpdateMediaResponse)!;
                Assert(obj.Updated, "updated should be true");
                var roundTrip = JsonSerializer.Serialize(obj, AppJsonContext.Default.UpdateMediaResponse);
                Assert(roundTrip.Contains("\"updated\""), "updated key missing");
            });

            Check("BatchUpdateResponse round-trip", () =>
            {
                var obj = new BatchUpdateResponse { Success = true, Updates = 3 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BatchUpdateResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BatchUpdateResponse)!;
                Assert(back.Success, "success should be true");
                Assert(back.Updates == 3, $"updates: {back.Updates}");
            });

            Check("BatchGetResponse round-trip", () =>
            {
                var json = """{"libraryItems":[{"id":"li_1"},{"id":"li_2"}]}""";
                var obj = JsonSerializer.Deserialize(json, AppJsonContext.Default.BatchGetResponse)!;
                Assert(obj.LibraryItems.Count == 2, $"count: {obj.LibraryItems.Count}");
            });

            Check("SeriesItem round-trip", () =>
            {
                var obj = new SeriesItem { Id = "se_1", Name = "Mistborn", LibraryId = "lib_1" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.SeriesItem);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.SeriesItem)!;
                Assert(back.Id == "se_1", $"id: {back.Id}");
                Assert(back.Name == "Mistborn", $"name: {back.Name}");
            });

            Check("AuthorItem round-trip", () =>
            {
                var obj = new AuthorItem { Id = "aut_1", Name = "Sanderson", LibraryId = "lib_1", NumBooks = 4 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorItem);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorItem)!;
                Assert(back.Id == "aut_1", $"id: {back.Id}");
                Assert(back.Name == "Sanderson", $"name: {back.Name}");
                Assert(back.NumBooks == 4, $"numBooks: {back.NumBooks}");
            });

            Check("AuthorListResponse round-trip", () =>
            {
                var obj = new AuthorListResponse
                {
                    Authors = new List<AuthorItem> { new() { Id = "aut_1", Name = "Test" } }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorListResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorListResponse)!;
                Assert(back.Authors.Count == 1, $"count: {back.Authors.Count}");
                Assert(back.Authors[0].Name == "Test", $"name: {back.Authors[0].Name}");
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
