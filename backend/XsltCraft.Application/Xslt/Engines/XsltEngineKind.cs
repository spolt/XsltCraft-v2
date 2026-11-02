namespace XsltCraft.Application.Xslt.Engines;

/// <summary>Bir XSLT'yi derleyip çalıştıran somut motor.</summary>
public enum XsltEngineKind
{
    /// <summary>.NET <c>XslCompiledTransform</c> — yalnız XSLT 1.0.</summary>
    DotNet,

    /// <summary>Saxon HE — XSLT 2.0 (yalnız izole worker sürecinde yüklenir).</summary>
    Saxon,
}
