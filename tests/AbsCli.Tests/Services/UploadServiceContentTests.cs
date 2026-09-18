using AbsCli.Services;
using Xunit;

namespace AbsCli.Tests.Services;

public class UploadServiceContentTests
{
    // .NET's byte[] cap is 2 GiB; go past it so the old File.ReadAllBytesAsync
    // path throws IO_FileTooLong2GB. Sparse via SetLength, so this allocates no
    // blocks on ext4 (unit tests run on ubuntu-latest only).
    private const long OverTwoGigabytes = 2_200_000_000L;

    [Fact]
    public async Task BuildUploadContent_StreamsFileLargerThanTwoGigabytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"abs-cli-big-{Guid.NewGuid():N}.m4b");
        try
        {
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                fs.SetLength(OverTwoGigabytes);

            using var content = UploadService.BuildUploadContent(
                "lib-1", "fold-1", "Big Book", "Someone", null,
                new[] { (LocalPath: path, UploadName: "big.m4b") });

            await content.CopyToAsync(Stream.Null);

            Assert.NotNull(content.Headers.ContentLength);
            Assert.True(content.Headers.ContentLength > OverTwoGigabytes,
                $"expected a body larger than the file, got {content.Headers.ContentLength}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BuildUploadContent_AddsOnePartPerFileAndOmitsNullFields()
    {
        var a = Path.Combine(Path.GetTempPath(), $"abs-cli-a-{Guid.NewGuid():N}.mp3");
        var b = Path.Combine(Path.GetTempPath(), $"abs-cli-b-{Guid.NewGuid():N}.mp3");
        try
        {
            File.WriteAllText(a, "a");
            File.WriteAllText(b, "b");

            using var content = UploadService.BuildUploadContent(
                "lib-1", "fold-1", "Two Parter", null, null,
                new[] { (LocalPath: a, UploadName: "01.mp3"), (LocalPath: b, UploadName: "02.mp3") });

            var names = content
                .Select(p => p.Headers.ContentDisposition!.Name!.Trim('"'))
                .ToList();
            // library/folder/title always; no author or series part when null; one
            // part per file, named by index.
            Assert.Equal(new[] { "library", "folder", "title", "0", "1" }, names);
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }
}
