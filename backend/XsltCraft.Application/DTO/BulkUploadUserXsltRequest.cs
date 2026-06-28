namespace XsltCraft.Application.DTO;

/// <summary>Klasör olarak toplu XSLT yükleme isteği.</summary>
public sealed class BulkUploadUserXsltRequest
{
    /// <summary>Hedef klasör (yoksa kök/klasörsüz). Sahip + Kind=XsltTemplate olmalı.</summary>
    public Guid? FolderId { get; set; }

    public List<BulkUploadItem> Items { get; set; } = [];
}

public sealed class BulkUploadItem
{
    public string Name { get; set; } = string.Empty;
    public string XsltContent { get; set; } = string.Empty;
}

public sealed class BulkUploadResultResponse
{
    public List<BulkUploadCreated> Created { get; set; } = [];
    public List<BulkUploadSkipped> Skipped { get; set; } = [];
}

public sealed class BulkUploadCreated
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class BulkUploadSkipped
{
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
