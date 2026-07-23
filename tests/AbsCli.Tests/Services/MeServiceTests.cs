using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class MeServiceTests
{
    [Fact]
    public void MediaProgress_RoundTrip_Audio()
    {
        var obj = new MediaProgress
        {
            Id = "mp_1",
            UserId = "u_1",
            LibraryItemId = "li_abc",
            EpisodeId = null,
            MediaItemId = "b_abc",
            MediaItemType = "book",
            Duration = 3600.5,
            Progress = 0.5,
            CurrentTime = 1800.25,
            IsFinished = false,
            HideFromContinueListening = false,
            EbookLocation = null,
            EbookProgress = null,
            LastUpdate = 1716000000000,
            StartedAt = 1715000000000,
            FinishedAt = null
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.MediaProgress);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MediaProgress)!;
        Assert.Equal("mp_1", back.Id);
        Assert.Equal("li_abc", back.LibraryItemId);
        Assert.Equal(3600.5, back.Duration);
        Assert.Equal(1800.25, back.CurrentTime);
        Assert.False(back.IsFinished);
        Assert.Null(back.EbookLocation);
        Assert.Null(back.FinishedAt);
    }

    [Fact]
    public void MediaProgress_RoundTrip_FinishedEbook()
    {
        var json = """
        {"id":"mp_2","userId":"u_1","libraryItemId":"li_x","episodeId":null,
         "mediaItemId":"b_x","mediaItemType":"book","duration":0,"progress":1,
         "currentTime":0,"isFinished":true,"hideFromContinueListening":false,
         "ebookLocation":"epubcfi(/6/4!/4)","ebookProgress":1,
         "lastUpdate":1716000000000,"startedAt":1715000000000,"finishedAt":1716100000000}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MediaProgress)!;
        Assert.True(back.IsFinished);
        Assert.Equal("epubcfi(/6/4!/4)", back.EbookLocation);
        Assert.Equal(1.0, back.EbookProgress);
        Assert.Equal(1716100000000, back.FinishedAt);
    }

    [Fact]
    public void MediaProgress_Deserializes_NumericEbookLocation()
    {
        // ABS declares ebookLocation as STRING but SQLite type affinity lets
        // legacy numeric values leak through as bare JSON numbers (issue #65).
        // The model must tolerate them, surfacing the raw number as a string.
        var json = """
        {"id":"mp_3","userId":"u_1","libraryItemId":"li_y","mediaItemId":"b_y",
         "mediaItemType":"book","duration":0,"progress":0,"currentTime":0,
         "isFinished":false,"hideFromContinueListening":false,
         "ebookLocation":24,"ebookProgress":0,
         "lastUpdate":0,"startedAt":0,"finishedAt":null}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MediaProgress)!;
        Assert.Equal("24", back.EbookLocation);
    }

    [Fact]
    public void MediaProgress_Deserializes_StringEbookLocation_Unchanged()
    {
        // The common case — a locator/CFI string — must still pass through verbatim.
        var json = """
        {"id":"mp_4","userId":"u_1","libraryItemId":"li_z","mediaItemId":"b_z",
         "mediaItemType":"book","duration":0,"progress":0,"currentTime":0,
         "isFinished":false,"hideFromContinueListening":false,
         "ebookLocation":"{\"href\":\"x.jpeg\"}","ebookProgress":0,
         "lastUpdate":0,"startedAt":0,"finishedAt":null}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MediaProgress)!;
        Assert.Equal("{\"href\":\"x.jpeg\"}", back.EbookLocation);
    }

    [Fact]
    public void MediaProgress_Deserializes_NullEbookLocation()
    {
        var json = """
        {"id":"mp_5","userId":"u_1","libraryItemId":"li_n","mediaItemId":"b_n",
         "mediaItemType":"book","duration":0,"progress":0,"currentTime":0,
         "isFinished":false,"hideFromContinueListening":false,
         "ebookLocation":null,"ebookProgress":0,
         "lastUpdate":0,"startedAt":0,"finishedAt":null}
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.MediaProgress)!;
        Assert.Null(back.EbookLocation);
    }

    [Fact]
    public void ProgressUpdateRequest_OmitsUnsetFields()
    {
        // Only is-finished set; CurrentTime/EbookLocation/etc. should be absent from JSON.
        var obj = new ProgressUpdateRequest { IsFinished = true };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ProgressUpdateRequest);
        Assert.Contains("\"isFinished\": true", json);
        Assert.DoesNotContain("currentTime", json);
        Assert.DoesNotContain("ebookLocation", json);
        Assert.DoesNotContain("ebookProgress", json);
        Assert.DoesNotContain("hideFromContinueListening", json);
        Assert.DoesNotContain("finishedAt", json);
    }

    [Fact]
    public void ProgressUpdateRequest_SerializesAllFields()
    {
        var obj = new ProgressUpdateRequest
        {
            CurrentTime = 123.45,
            IsFinished = true,
            EbookLocation = "epubcfi(/6/4!/4)",
            EbookProgress = 0.75,
            HideFromContinueListening = false,
            FinishedAt = 1716100000000
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ProgressUpdateRequest);
        Assert.Contains("\"currentTime\": 123.45", json);
        Assert.Contains("\"isFinished\": true", json);
        Assert.Contains("\"ebookLocation\": \"epubcfi(/6/4!/4)\"", json);
        Assert.Contains("\"ebookProgress\": 0.75", json);
        Assert.Contains("\"hideFromContinueListening\": false", json);
        Assert.Contains("\"finishedAt\": 1716100000000", json);
    }

    [Fact]
    public void ProgressUpdateRequest_EmptyEbookLocationSerializes()
    {
        // Empty string is the "clear" signal — must serialize, not be omitted.
        var obj = new ProgressUpdateRequest { EbookLocation = "" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ProgressUpdateRequest);
        Assert.Contains("\"ebookLocation\": \"\"", json);
    }

    [Fact]
    public void Me_RoundTrip_Minimal()
    {
        var obj = new Me
        {
            Id = "u_1",
            Username = "testuser",
            Email = "test@example.com",
            Type = "user",
            Token = "abc",
            IsActive = true,
            LastSeen = 1716000000000,
            CreatedAt = 1715000000000,
            Permissions = new UserPermissions { Update = true, Delete = false }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.Me);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Me)!;
        Assert.Equal("u_1", back.Id);
        Assert.Equal("testuser", back.Username);
        Assert.Equal("test@example.com", back.Email);
        Assert.True(back.IsActive);
        Assert.NotNull(back.Permissions);
        Assert.True(back.Permissions!.Update);
        Assert.False(back.Permissions.Delete);
    }

    [Fact]
    public void Me_Deserializes_FullPayload()
    {
        // Simulates ABS's actual /api/me response including arrays and
        // bookmarks (round-tripped as JsonElement, never typed).
        var json = """
        {
          "id":"u_1","username":"testuser","email":null,"type":"user",
          "token":"t","isOldToken":false,
          "permissions":{"download":true,"update":true,"delete":false,
                         "upload":true,"accessAllLibraries":true,
                         "accessAllTags":true,"accessExplicitContent":true},
          "librariesAccessible":[],"itemTagsSelected":[],
          "mediaProgress":[],"bookmarks":[{"libraryItemId":"li_1","time":42}],
          "seriesHideFromContinueListening":[],
          "isActive":true,"isLocked":false,"lastSeen":1716000000000,
          "createdAt":1715000000000,"hasOpenIDLink":false
        }
        """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.Me)!;
        Assert.Equal("u_1", back.Id);
        Assert.Single(back.Bookmarks);
        Assert.NotNull(back.Permissions);
        Assert.True(back.Permissions!.Download);
    }

    [Fact]
    public void UserPermissions_Extra_PreservesUnknownFields()
    {
        // ABS may add new permission keys; CLI must round-trip them via the
        // extension-data catch-all.
        var json = """{"update":true,"delete":false,"customNewPerm":true}""";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserPermissions)!;
        Assert.True(back.Update);
        Assert.NotNull(back.Extra);
        Assert.True(back.Extra!.ContainsKey("customNewPerm"));
        var roundtripped = JsonSerializer.Serialize(back, AppJsonContext.Default.UserPermissions);
        Assert.Contains("customNewPerm", roundtripped);
    }
}
