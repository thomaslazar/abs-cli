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

        command.SetAction(parseResult =>
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

            Check("PaginatedResponse (authors) round-trip", () =>
            {
                var obj = new PaginatedResponse { Total = 1, Limit = 50, Page = 0 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.PaginatedResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.PaginatedResponse)!;
                Assert(back.Total == 1, $"total: {back.Total}");
                Assert(back.Limit == 50, $"limit: {back.Limit}");
                Assert(back.Page == 0, $"page: {back.Page}");
            });

            Check("Collection round-trip", () =>
            {
                var obj = new Collection
                {
                    Id = "col_x",
                    LibraryId = "lib_1",
                    Name = "set",
                    Description = null,
                    Books = new(),
                    LastUpdate = 1,
                    CreatedAt = 0
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Collection);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Collection)!;
                Assert(back.Id == "col_x", $"id: {back.Id}");
            });

            Check("CollectionCreateRequest round-trip", () =>
            {
                var obj = new CollectionCreateRequest { LibraryId = "lib_1", Name = "n", Books = new() { "li_a" } };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionCreateRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CollectionCreateRequest)!;
                Assert(back.Books[0] == "li_a", $"books: {back.Books[0]}");
            });

            Check("CollectionBooksRequest round-trip", () =>
            {
                var obj = new CollectionBooksRequest { Books = new() { "li_a", "li_b" } };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionBooksRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CollectionBooksRequest)!;
                Assert(back.Books.Count == 2, $"count: {back.Books.Count}");
            });

            Check("CollectionBookRequest round-trip", () =>
            {
                var obj = new CollectionBookRequest { Id = "li_z" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CollectionBookRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CollectionBookRequest)!;
                Assert(back.Id == "li_z", $"id: {back.Id}");
            });

            Check("Me round-trip", () =>
            {
                var obj = new Me
                {
                    Id = "u_1",
                    Username = "testuser",
                    Type = "user",
                    IsActive = true,
                    CreatedAt = 0
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Me);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Me)!;
                Assert(back.Username == "testuser", $"username: {back.Username}");
            });

            Check("MediaProgress round-trip", () =>
            {
                var obj = new MediaProgress
                {
                    Id = "mp_1",
                    UserId = "u_1",
                    MediaItemId = "b_1",
                    MediaItemType = "book",
                    IsFinished = true
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.MediaProgress);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MediaProgress)!;
                Assert(back.IsFinished, $"isFinished: {back.IsFinished}");
            });

            Check("ProgressUpdateRequest round-trip", () =>
            {
                var obj = new ProgressUpdateRequest { IsFinished = true };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ProgressUpdateRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ProgressUpdateRequest)!;
                Assert(back.IsFinished == true, $"isFinished: {back.IsFinished}");
            });

            Check("RssFeed round-trip", () =>
            {
                var obj = new RssFeed { Id = "feed_1", Slug = "s" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.RssFeed);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.RssFeed)!;
                Assert(back.Id == "feed_1", $"id: {back.Id}");
            });

            Check("BackupItem round-trip", () =>
            {
                var obj = new BackupItem
                {
                    Id = "2024-03-15T1430",
                    Filename = "2024-03-15T1430.audiobookshelf",
                    FileSize = 12345,
                    CreatedAt = 1710510600000,
                    ServerVersion = "2.33.1"
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BackupItem);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BackupItem)!;
                Assert(back.Id == "2024-03-15T1430", $"id: {back.Id}");
                Assert(back.FileSize == 12345, $"fileSize: {back.FileSize}");
                Assert(back.ServerVersion == "2.33.1", $"serverVersion: {back.ServerVersion}");
            });

            Check("BackupListResponse round-trip", () =>
            {
                var obj = new BackupListResponse
                {
                    Backups = new List<BackupItem>
                    {
                        new() { Id = "bk_1", Filename = "test.audiobookshelf" }
                    },
                    BackupLocation = "/backups",
                    BackupPathEnvSet = false
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BackupListResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BackupListResponse)!;
                Assert(back.Backups.Count == 1, $"count: {back.Backups.Count}");
                Assert(back.BackupLocation == "/backups", $"location: {back.BackupLocation}");
            });

            Check("ScanResult round-trip", () =>
            {
                var obj = new ScanResult { Result = "UPDATED" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ScanResult);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ScanResult)!;
                Assert(back.Result == "UPDATED", $"result: {back.Result}");
            });

            Check("TaskItem round-trip", () =>
            {
                var json = """{"id":"task_1","action":"library-scan","title":"Scanning","isFailed":false,"isFinished":true,"startedAt":1000,"finishedAt":2000,"data":{"added":5}}""";
                var obj = JsonSerializer.Deserialize(json, AppJsonContext.Default.TaskItem)!;
                Assert(obj.Id == "task_1", $"id: {obj.Id}");
                Assert(obj.Action == "library-scan", $"action: {obj.Action}");
                Assert(obj.IsFinished, "isFinished should be true");
                Assert(obj.Data.HasValue, "data should have value");
                var roundTrip = JsonSerializer.Serialize(obj, AppJsonContext.Default.TaskItem);
                Assert(roundTrip.Contains("\"action\""), "action key missing in output");
            });

            Check("TaskListResponse round-trip", () =>
            {
                var obj = new TaskListResponse
                {
                    Tasks = new List<TaskItem> { new() { Id = "t_1", Action = "scan" } }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.TaskListResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.TaskListResponse)!;
                Assert(back.Tasks.Count == 1, $"count: {back.Tasks.Count}");
                Assert(back.Tasks[0].Action == "scan", $"action: {back.Tasks[0].Action}");
            });

            Check("MetadataProvidersResponse round-trip", () =>
            {
                var obj = new MetadataProvidersResponse
                {
                    Providers = new MetadataProviderGroups
                    {
                        Books = new List<ProviderEntry>
                        {
                            new() { Value = "google", Text = "Google Books" }
                        },
                        BooksCovers = new List<ProviderEntry>
                        {
                            new() { Value = "best", Text = "Best" }
                        },
                        Podcasts = new List<ProviderEntry>()
                    }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.MetadataProvidersResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MetadataProvidersResponse)!;
                Assert(back.Providers.Books.Count == 1, $"books count: {back.Providers.Books.Count}");
                Assert(back.Providers.Books[0].Value == "google", $"value: {back.Providers.Books[0].Value}");
                Assert(back.Providers.Books[0].Text == "Google Books", $"text: {back.Providers.Books[0].Text}");
            });

            Check("CoverSearchResponse round-trip", () =>
            {
                var obj = new CoverSearchResponse
                {
                    Results = new List<string> { "https://example.com/cover1.jpg", "https://example.com/cover2.jpg" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverSearchResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverSearchResponse)!;
                Assert(back.Results.Count == 2, $"count: {back.Results.Count}");
                Assert(back.Results[0] == "https://example.com/cover1.jpg", $"url: {back.Results[0]}");
            });

            Check("UploadManifestEntry list round-trip", () =>
            {
                var json = """[{"src":"Part 1-2/foo.mp3","as":"001-foo.mp3"},{"src":"Part 3/foo.mp3","as":"002-foo.mp3"}]""";
                var entries = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListUploadManifestEntry)!;
                Assert(entries.Count == 2, $"count: {entries.Count}");
                Assert(entries[0].Src == "Part 1-2/foo.mp3", $"src: {entries[0].Src}");
                Assert(entries[0].TargetName == "001-foo.mp3", $"as: {entries[0].TargetName}");
                Assert(entries[1].TargetName == "002-foo.mp3", $"as: {entries[1].TargetName}");
                var roundTrip = JsonSerializer.Serialize(entries, AppJsonContext.Default.ListUploadManifestEntry);
                Assert(roundTrip.Contains("\"src\""), "src key missing in output");
                Assert(roundTrip.Contains("\"as\""), "as key missing in output");
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

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Cover Models ===");

            Check("CoverApplyResponse round-trip", () =>
            {
                var obj = new CoverApplyResponse { Success = true, Cover = "/srv/abs/covers/foo.jpg" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyResponse)!;
                Assert(back.Success == true, $"success: {back.Success}");
                Assert(back.Cover == "/srv/abs/covers/foo.jpg", $"cover: {back.Cover}");
            });

            Check("CoverApplyByUrlRequest round-trip", () =>
            {
                var obj = new CoverApplyByUrlRequest { Url = "https://example.com/cover.jpg" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyByUrlRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyByUrlRequest)!;
                Assert(back.Url == "https://example.com/cover.jpg", $"url: {back.Url}");
            });

            Check("CoverLinkExistingRequest round-trip", () =>
            {
                var obj = new CoverLinkExistingRequest { Cover = "/srv/abs/library/Author/Title/cover.jpg" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverLinkExistingRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverLinkExistingRequest)!;
                Assert(back.Cover == "/srv/abs/library/Author/Title/cover.jpg", $"cover: {back.Cover}");
            });

            Check("CoverFileSavedDescriptor round-trip", () =>
            {
                var obj = new CoverFileSavedDescriptor { Path = "/tmp/cover.jpg", Bytes = 12345 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverFileSavedDescriptor);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverFileSavedDescriptor)!;
                Assert(back.Path == "/tmp/cover.jpg", $"path: {back.Path}");
                Assert(back.Bytes == 12345, $"bytes: {back.Bytes}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Encode M4B Models ===");

            Check("EncodeM4bOptions round-trip (all fields set)", () =>
            {
                var obj = new EncodeM4bOptions { Codec = "aac", Bitrate = "128k", Channels = 2 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bOptions);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EncodeM4bOptions)!;
                Assert(back.Codec == "aac", $"codec: {back.Codec}");
                Assert(back.Bitrate == "128k", $"bitrate: {back.Bitrate}");
                Assert(back.Channels == 2, $"channels: {back.Channels}");
                Assert(!json.Contains("null"), $"unset fields should be omitted, got: {json}");
            });

            Check("EncodeM4bOptions round-trip (omits unset fields)", () =>
            {
                var obj = new EncodeM4bOptions { Codec = "copy" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bOptions);
                Assert(json.Contains("\"codec\": \"copy\""), $"codec missing: {json}");
                Assert(!json.Contains("bitrate"), $"bitrate should be omitted: {json}");
                Assert(!json.Contains("channels"), $"channels should be omitted: {json}");
            });

            Check("EncodeM4bStartReceipt round-trip", () =>
            {
                var obj = new EncodeM4bStartReceipt
                {
                    LibraryItemId = "li_abc123",
                    Action = "encode-m4b",
                    Started = true,
                    Options = new EncodeM4bOptions { Codec = "copy" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EncodeM4bStartReceipt);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EncodeM4bStartReceipt)!;
                Assert(back.LibraryItemId == "li_abc123", $"libraryItemId: {back.LibraryItemId}");
                Assert(back.Action == "encode-m4b", $"action: {back.Action}");
                Assert(back.Started == true, $"started: {back.Started}");
                Assert(back.Options.Codec == "copy", $"options.codec: {back.Options.Codec}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Chapter Models ===");

            Check("ChaptersLookupResponse round-trip", () =>
            {
                var obj = new ChaptersLookupResponse
                {
                    Asin = "B07TEST1",
                    Chapters = new List<AudnexusChapter>
                    {
                        new() { Title = "Ch 1", LengthMs = 12345, StartOffsetMs = 0, StartOffsetSec = 0 }
                    },
                    IsAccurate = true,
                    RuntimeLengthSec = 50.0
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersLookupResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersLookupResponse)!;
                Assert(back.Asin == "B07TEST1", $"asin: {back.Asin}");
                Assert(back.Chapters.Count == 1, $"chapter count: {back.Chapters.Count}");
                Assert(back.Chapters[0].Title == "Ch 1", $"chapter title: {back.Chapters[0].Title}");
                Assert(back.IsAccurate == true, $"isAccurate: {back.IsAccurate}");
            });

            Check("ChaptersLookupError round-trip", () =>
            {
                var obj = new ChaptersLookupError { Error = "Chapters not found", StringKey = "MessageChaptersNotFound" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersLookupError);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersLookupError)!;
                Assert(back.Error == "Chapters not found", $"error: {back.Error}");
                Assert(back.StringKey == "MessageChaptersNotFound", $"stringKey: {back.StringKey}");
            });

            Check("ChaptersSetRequest round-trip", () =>
            {
                var obj = new ChaptersSetRequest
                {
                    Chapters = new List<ChapterWriteEntry>
                    {
                        new() { Title = "Intro", Start = 0, End = 1.5 }
                    }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersSetRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersSetRequest)!;
                Assert(back.Chapters.Count == 1, $"count: {back.Chapters.Count}");
                Assert(back.Chapters[0].Title == "Intro", $"title: {back.Chapters[0].Title}");
                Assert(back.Chapters[0].End == 1.5, $"end: {back.Chapters[0].End}");
            });

            Check("ChaptersSetResponse round-trip", () =>
            {
                var obj = new ChaptersSetResponse { Success = true, Updated = false };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ChaptersSetResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersSetResponse)!;
                Assert(back.Success == true, $"success: {back.Success}");
                Assert(back.Updated == false, $"updated: {back.Updated}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Embed Metadata Models ===");

            Check("EmbedMetadataOptions default round-trip", () =>
            {
                var obj = new EmbedMetadataOptions();
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EmbedMetadataOptions);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EmbedMetadataOptions)!;
                Assert(back.Backup == true, $"backup default: {back.Backup}");
                Assert(back.ForceEmbedChapters == false, $"forceEmbedChapters default: {back.ForceEmbedChapters}");
            });

            Check("EmbedMetadataReceipt round-trip", () =>
            {
                var obj = new EmbedMetadataReceipt
                {
                    LibraryItemId = "li_abc123",
                    Started = true,
                    Options = new EmbedMetadataOptions { Backup = false, ForceEmbedChapters = true }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EmbedMetadataReceipt);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EmbedMetadataReceipt)!;
                Assert(back.LibraryItemId == "li_abc123", $"libraryItemId: {back.LibraryItemId}");
                Assert(back.Action == "embed-metadata", $"action: {back.Action}");
                Assert(back.Started == true, $"started: {back.Started}");
                Assert(back.Options.Backup == false, $"options.backup: {back.Options.Backup}");
                Assert(back.Options.ForceEmbedChapters == true, $"options.forceEmbedChapters: {back.Options.ForceEmbedChapters}");
            });

            Check("BatchEmbedMetadataRequest round-trip", () =>
            {
                var obj = new BatchEmbedMetadataRequest { LibraryItemIds = new List<string> { "li_a", "li_b" } };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BatchEmbedMetadataRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BatchEmbedMetadataRequest)!;
                Assert(back.LibraryItemIds.Count == 2, $"count: {back.LibraryItemIds.Count}");
                Assert(back.LibraryItemIds[0] == "li_a", $"first: {back.LibraryItemIds[0]}");
            });

            Check("BatchEmbedMetadataReceipt round-trip", () =>
            {
                var obj = new BatchEmbedMetadataReceipt
                {
                    Started = true,
                    LibraryItemIds = new List<string> { "li_a" },
                    Options = new EmbedMetadataOptions { Backup = true, ForceEmbedChapters = true }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.BatchEmbedMetadataReceipt);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.BatchEmbedMetadataReceipt)!;
                Assert(back.Action == "embed-metadata", $"action: {back.Action}");
                Assert(back.Started == true, $"started: {back.Started}");
                Assert(back.LibraryItemIds.Count == 1, $"count: {back.LibraryItemIds.Count}");
                Assert(back.Options.ForceEmbedChapters == true, $"forceEmbedChapters: {back.Options.ForceEmbedChapters}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Ebook File Models ===");

            Check("EbookFileStatusReceipt round-trip", () =>
            {
                var obj = new EbookFileStatusReceipt
                {
                    LibraryItemId = "li_abc123",
                    FileIno = "12345678",
                    Toggled = true
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.EbookFileStatusReceipt);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.EbookFileStatusReceipt)!;
                Assert(back.LibraryItemId == "li_abc123", $"libraryItemId: {back.LibraryItemId}");
                Assert(back.FileIno == "12345678", $"fileIno: {back.FileIno}");
                Assert(back.Action == "toggle-ebook-status", $"action: {back.Action}");
                Assert(back.Toggled == true, $"toggled: {back.Toggled}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Expanded Item Models ===");

            Check("LibraryFileMetadata round-trip", () =>
            {
                var obj = new LibraryFileMetadata
                {
                    Filename = "multi.epub",
                    Ext = ".epub",
                    Path = "/audiobooks/Author/Title/multi.epub",
                    RelPath = "multi.epub",
                    Size = 1216,
                    MtimeMs = 1779100661814,
                    CtimeMs = 1779100661814,
                    BirthtimeMs = 1779100661814
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.LibraryFileMetadata);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryFileMetadata)!;
                Assert(back.Filename == "multi.epub", $"filename: {back.Filename}");
                Assert(back.Ext == ".epub", $"ext: {back.Ext}");
                Assert(back.Size == 1216, $"size: {back.Size}");
            });

            Check("LibraryFile round-trip", () =>
            {
                var obj = new LibraryFile
                {
                    Ino = "16400001",
                    Metadata = new LibraryFileMetadata { Filename = "multi.epub", Ext = ".epub" },
                    AddedAt = 1779100661871,
                    UpdatedAt = 1779100661871,
                    IsSupplementary = false,
                    FileType = "ebook"
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.LibraryFile);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryFile)!;
                Assert(back.Ino == "16400001", $"ino: {back.Ino}");
                Assert(back.FileType == "ebook", $"fileType: {back.FileType}");
                Assert(back.IsSupplementary == false, $"isSupplementary: {back.IsSupplementary}");
                Assert(back.Metadata.Filename == "multi.epub", $"metadata.filename: {back.Metadata.Filename}");
            });

            Check("LibraryItemExpanded round-trip", () =>
            {
                var obj = new LibraryItemExpanded
                {
                    Id = "li_xyz",
                    LibraryId = "lib_xyz",
                    MediaType = "book",
                    LastScan = 1779100662000,
                    ScanVersion = "2.33.2",
                    LibraryFiles = new List<LibraryFile>
                    {
                        new() { Ino = "16400001", FileType = "ebook", IsSupplementary = false },
                        new() { Ino = "16400002", FileType = "ebook", IsSupplementary = true }
                    }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.LibraryItemExpanded);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryItemExpanded)!;
                Assert(back.Id == "li_xyz", $"id: {back.Id}");
                Assert(back.LibraryFiles.Count == 2, $"libraryFiles count: {back.LibraryFiles.Count}");
                Assert(back.LibraryFiles[1].IsSupplementary == true, $"second is supplementary");
                Assert(back.ScanVersion == "2.33.2", $"scanVersion: {back.ScanVersion}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Author Models ===");

            Check("AuthorMatchRequest round-trip", () =>
            {
                var obj = new AuthorMatchRequest
                {
                    Q = "Brandon Sanderson",
                    Region = "us"
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorMatchRequest);
                Assert(json.Contains("\"q\""), $"q present: {json}");
                Assert(json.Contains("\"region\""), $"region present: {json}");
                Assert(!json.Contains("\"asin\""), $"asin absent (WhenWritingNull): {json}");
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorMatchRequest)!;
                Assert(back.Q == "Brandon Sanderson", $"q: {back.Q}");
                Assert(back.Region == "us", $"region: {back.Region}");
                Assert(back.Asin is null, $"asin: {back.Asin}");
            });

            Check("AuthorMatchResponse round-trip", () =>
            {
                var obj = new AuthorMatchResponse
                {
                    Updated = true,
                    Author = new AuthorItem { Id = "aut_xyz", Name = "Brandon Sanderson", Asin = "B000AP9DSU" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorMatchResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorMatchResponse)!;
                Assert(back.Updated == true, $"updated: {back.Updated}");
                Assert(back.Author?.Name == "Brandon Sanderson", $"author.name: {back.Author?.Name}");
                Assert(back.Author?.Asin == "B000AP9DSU", $"author.asin: {back.Author?.Asin}");
            });

            Check("AuthorUpdateResponse normal-shape round-trip", () =>
            {
                var obj = new AuthorUpdateResponse
                {
                    Updated = true,
                    Author = new AuthorItem { Id = "aut_xyz", Name = "Brandon Sanderson" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorUpdateResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorUpdateResponse)!;
                Assert(back.Updated == true, $"updated: {back.Updated}");
                Assert(back.Merged is null, $"merged: {back.Merged}");
                Assert(back.Author?.Name == "Brandon Sanderson", $"author.name: {back.Author?.Name}");
            });

            Check("AuthorUpdateResponse merge-shape round-trip", () =>
            {
                var json = "{\"merged\":true,\"author\":{\"id\":\"aut_existing\",\"name\":\"Brandon Sanderson\"}}";
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorUpdateResponse)!;
                Assert(back.Merged == true, $"merged: {back.Merged}");
                Assert(back.Updated is null, $"updated: {back.Updated}");
                Assert(back.Author?.Id == "aut_existing", $"author.id: {back.Author?.Id}");
            });

            Check("Update-body Dictionary tri-state serialization", () =>
            {
                var body = new Dictionary<string, string>
                {
                    ["name"] = "Brandon Sanderson",
                    ["description"] = null!,
                    ["asin"] = "B000AP9DSU"
                };
                var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
                Assert(json.Contains("\"name\": \"Brandon Sanderson\""), $"name: {json}");
                Assert(json.Contains("\"description\": null"), $"description null: {json}");
                Assert(json.Contains("\"asin\": \"B000AP9DSU\""), $"asin: {json}");
            });

            Check("AuthorImageRequest round-trip", () =>
            {
                var obj = new AuthorImageRequest { Url = "https://example.com/img.png" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorImageRequest);
                Assert(json.Contains("\"url\": \"https://example.com/img.png\""), $"url: {json}");
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorImageRequest)!;
                Assert(back.Url == "https://example.com/img.png", $"url: {back.Url}");
            });

            Check("AuthorImageResponse round-trip", () =>
            {
                var obj = new AuthorImageResponse
                {
                    Author = new AuthorItem { Id = "aut_xyz", Name = "Brandon Sanderson", ImagePath = "/m/authors/x.jpg" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorImageResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorImageResponse)!;
                Assert(back.Author?.Name == "Brandon Sanderson", $"author.name: {back.Author?.Name}");
                Assert(back.Author?.ImagePath == "/m/authors/x.jpg", $"author.imagePath: {back.Author?.ImagePath}");
            });

            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Embedded resources ===");

            Check("CHANGELOG.md embedded and parseable", () =>
            {
                var latest = AbsCli.Services.ChangelogReader.ReadLatest();
                Assert(!string.IsNullOrWhiteSpace(latest), "ReadLatest returned empty");
                Assert(latest.StartsWith("## ", StringComparison.Ordinal),
                    $"ReadLatest did not start with '## ': '{latest[..Math.Min(40, latest.Length)]}'");
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
