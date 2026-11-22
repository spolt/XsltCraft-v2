namespace XsltCraft.Application.Templates.Download;

/// <summary>ZIP'e yazılacak tek şablon: ham ad (temizlenmemiş), XSLT metni ve son değişiklik zamanı.</summary>
public sealed record ArchiveEntrySource(string Name, string Content, DateTime? LastModifiedUtc);

/// <summary>
/// Şablonları ZIP arşivi olarak hedef akışa yazar; hedef kapatılmaz (<c>leaveOpen</c>). Girdi adları
/// <see cref="ArchiveFileNames"/> ile temizlenir ve tekilleştirilir; tüm girdiler ZIP kökündedir.
/// Hedef akışa senkron yazım da yapılabilir (bkz. <see cref="TemplateArchiveWriter"/>).
/// </summary>
public interface ITemplateArchiveWriter
{
    Task WriteAsync(IAsyncEnumerable<ArchiveEntrySource> entries, Stream destination, CancellationToken ct);
}
