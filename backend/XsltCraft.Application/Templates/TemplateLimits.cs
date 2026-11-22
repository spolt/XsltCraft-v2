namespace XsltCraft.Application.Templates;

/// <summary>"Şablonlarım" (UserXsltTemplate) sınırları.</summary>
public static class TemplateLimits
{
    /// <summary>Bir XSLT klasörüne eklenebilecek en fazla şablon (mevcut fazlalık bozulmaz; yalnız yeni ekleme engellenir).</summary>
    public const int MaxTemplatesPerFolder = 500;

    /// <summary>Toplu taşımada tek istekteki en fazla şablon.</summary>
    public const int MaxBulkMoveItems = 500;
}
