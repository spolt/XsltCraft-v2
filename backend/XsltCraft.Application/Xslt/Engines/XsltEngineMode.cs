namespace XsltCraft.Application.Xslt.Engines;

/// <summary>Motor seçim politikası (yapılandırmadan gelir).</summary>
public enum XsltEngineMode
{
    /// <summary>Stylesheet sürümüne göre: &gt;= 2.0 → Saxon, aksi → .NET.</summary>
    Auto,

    /// <summary>Her zaman .NET <c>XslCompiledTransform</c>.</summary>
    DotNet,

    /// <summary>Her zaman Saxon.</summary>
    Saxon,
}
