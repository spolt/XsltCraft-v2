using System.IO.Compression;
using System.Text;

namespace XsltCraft.Application.Templates.Download;

/// <summary>
/// <see cref="ITemplateArchiveWriter"/> uygulaması: .NET 10 async ZIP API'leriyle hedef akışa yazar. İçerik UTF-8 (BOM'suz),
/// XSLT metnine dokunulmaz. Not: .NET 10'da girdi akışının kapanışı (deflate son tamponu + veri tanımlayıcı) hedefe
/// <b>senkron</b> yazar; bu yüzden hedef ASP.NET yanıt gövdesi olmamalı — çağıran geçici dosyaya yazıp oradan sunar.
/// </summary>
public sealed class TemplateArchiveWriter : ITemplateArchiveWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly DateTime MinZipTimestamp = new(1980, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private const int WriterBufferSize = 16 * 1024;

    public async Task WriteAsync(IAsyncEnumerable<ArchiveEntrySource> entries, Stream destination, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(destination);

        var names = new ArchiveNameRegistry();

        await using var archive = await ZipArchive.CreateAsync(
            destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken: ct);

        await foreach (var source in entries.WithCancellation(ct))
        {
            var entry = archive.CreateEntry(names.Reserve(source.Name), CompressionLevel.Optimal);
            if (source.LastModifiedUtc is { } modified)
            {
                var utc = DateTime.SpecifyKind(modified, DateTimeKind.Utc);
                if (utc >= MinZipTimestamp)
                    entry.LastWriteTime = new DateTimeOffset(utc);
            }

            await using var entryStream = await entry.OpenAsync(ct);
            await using var writer = new StreamWriter(entryStream, Utf8NoBom, WriterBufferSize, leaveOpen: true);
            await writer.WriteAsync((source.Content ?? string.Empty).AsMemory(), ct);
            await writer.FlushAsync(ct);
        }
    }
}
