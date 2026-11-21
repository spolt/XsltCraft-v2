namespace XsltCraft.Application.DTO;

/// <summary>Seçili XSLT şablonlarını ZIP olarak indirme isteği (yalnız sahip olunanlar arşive girer).</summary>
public sealed class DownloadUserXsltRequest
{
    public List<Guid> Ids { get; set; } = [];
}
