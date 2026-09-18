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

            var counter = new CountingStream();
            await content.CopyToAsync(counter);

            Assert.NotNull(content.Headers.ContentLength);
            Assert.True(content.Headers.ContentLength > OverTwoGigabytes,
                $"expected a body larger than the file, got {content.Headers.ContentLength}");
            Assert.Equal(content.Headers.ContentLength, counter.BytesWritten);
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
            // File parts must carry no Content-Type: that is what keeps the wire
            // format identical to the old ByteArrayContent parts.
            Assert.All(content.Skip(3), p => Assert.Null(p.Headers.ContentType));
            Assert.Equal(new[] { "01.mp3", "02.mp3" },
                content.Skip(3).Select(p => p.Headers.ContentDisposition!.FileName!.Trim('"')));
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }

    private sealed class CountingStream : Stream
    {
        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => BytesWritten += count;
        public override void Write(ReadOnlySpan<byte> buffer) => BytesWritten += buffer.Length;

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            BytesWritten += count;
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BytesWritten += buffer.Length;
            return ValueTask.CompletedTask;
        }
    }
}
