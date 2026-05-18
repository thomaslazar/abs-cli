using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class LibraryItemExpandedTests
{
    [Fact]
    public void LibraryFileMetadata_RoundTrip()
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
        Assert.Equal("multi.epub", back.Filename);
        Assert.Equal(".epub", back.Ext);
        Assert.Equal(1216, back.Size);
    }

    [Fact]
    public void LibraryFile_EbookFields_RoundTrip()
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
        Assert.Equal("16400001", back.Ino);
        Assert.Equal("ebook", back.FileType);
        Assert.False(back.IsSupplementary);
        Assert.Equal("multi.epub", back.Metadata.Filename);
    }

    [Fact]
    public void LibraryFile_AudioFields_NullSupplementary()
    {
        // Non-ebook files leave isSupplementary unset; deserialization yields null.
        var json = "{\"ino\":\"16400099\",\"fileType\":\"audio\",\"metadata\":{}}";
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryFile)!;
        Assert.Equal("audio", back.FileType);
        Assert.Null(back.IsSupplementary);
    }

    [Fact]
    public void LibraryItemExpanded_RoundTrip()
    {
        var obj = new LibraryItemExpanded
        {
            Id = "li_xyz",
            LibraryId = "lib_xyz",
            MediaType = "book",
            LastScan = 1779100662000,
            ScanVersion = "2.33.2",
            OldLibraryItemId = null,
            LibraryFiles = new List<LibraryFile>
            {
                new() { Ino = "16400001", FileType = "ebook", IsSupplementary = false },
                new() { Ino = "16400002", FileType = "ebook", IsSupplementary = true }
            }
        };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.LibraryItemExpanded);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryItemExpanded)!;
        Assert.Equal("li_xyz", back.Id);
        Assert.Equal(2, back.LibraryFiles.Count);
        Assert.Equal("16400001", back.LibraryFiles[0].Ino);
        Assert.True(back.LibraryFiles[1].IsSupplementary);
        Assert.Equal("2.33.2", back.ScanVersion);
    }

    [Fact]
    public void LibraryItemExpanded_DeserializesRealABSExpandedBody()
    {
        // Trimmed real expanded body from the dev stack — checks the actual
        // ABS field names and shapes deserialize without throwing.
        var json = """
            {
              "id": "7f0014d2-cccf-4c32-80d5-604dd1315ed5",
              "ino": "16403841",
              "oldLibraryItemId": null,
              "libraryId": "lib_xyz",
              "folderId": "f_xyz",
              "path": "/audiobooks/Ebook Smoke Author/Multi Ebook Test",
              "relPath": "Multi Ebook Test",
              "isFile": false,
              "mtimeMs": 1779100661814,
              "ctimeMs": 1779100661814,
              "birthtimeMs": 1779100661814,
              "addedAt": 1779100661871,
              "updatedAt": 1779100661871,
              "lastScan": 1779100662000,
              "scanVersion": "2.33.2",
              "isMissing": false,
              "isInvalid": false,
              "mediaType": "book",
              "media": {"id":"m_xyz","metadata":{"title":"Multi Ebook Test"}},
              "libraryFiles": [
                {
                  "ino": "16403842",
                  "metadata": {
                    "filename": "multi.epub",
                    "ext": ".epub",
                    "path": "/audiobooks/Ebook Smoke Author/Multi Ebook Test/multi.epub",
                    "relPath": "multi.epub",
                    "size": 1216,
                    "mtimeMs": 1779100661814,
                    "ctimeMs": 1779100661814,
                    "birthtimeMs": 1779100661814
                  },
                  "addedAt": 1779100661871,
                  "updatedAt": 1779100661871,
                  "isSupplementary": false,
                  "fileType": "ebook"
                }
              ],
              "size": 1515
            }
            """;
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.LibraryItemExpanded)!;
        Assert.Equal("7f0014d2-cccf-4c32-80d5-604dd1315ed5", back.Id);
        Assert.Single(back.LibraryFiles);
        Assert.Equal("multi.epub", back.LibraryFiles[0].Metadata.Filename);
        Assert.Equal("ebook", back.LibraryFiles[0].FileType);
        Assert.False(back.LibraryFiles[0].IsSupplementary);
    }
}
