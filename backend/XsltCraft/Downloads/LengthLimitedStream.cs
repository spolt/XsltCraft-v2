namespace XsltCraft.Api.Downloads;

/// <summary>Arşiv bayt tavanı aşıldığında fırlatılır (413'e çevrilir).</summary>
public sealed class ArchiveTooLargeException : Exception;

/// <summary>
/// Yazma sarmalayıcısı: alttaki akışın uzunluğu <paramref name="maxBytes"/>'ı aşarsa yazmayı keser.
/// ZipArchive başlıkları geri dönüp yazdığı için arama (seek) alttaki akışa aynen iletilir.
/// </summary>
public sealed class LengthLimitedStream(Stream inner, long maxBytes) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) { Guard(value); inner.SetLength(value); }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Guard(inner.Position + count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Guard(inner.Position + buffer.Length);
        inner.Write(buffer);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Guard(inner.Position + buffer.Length);
        await inner.WriteAsync(buffer, cancellationToken);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    // Alttaki akış çağıranındır; sarmalayıcı kapatmaz.
    protected override void Dispose(bool disposing) { }

    private void Guard(long resultingEnd)
    {
        if (resultingEnd > maxBytes) throw new ArchiveTooLargeException();
    }
}
