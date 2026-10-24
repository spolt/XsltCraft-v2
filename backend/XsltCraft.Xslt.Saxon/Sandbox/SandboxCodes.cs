namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>Sandbox ihlallerinin <c>XsltDiagnostic.ErrorCode</c> değerleri (motor-bağımsız, sabit).</summary>
public static class SandboxCodes
{
    /// <summary>Dış kaynak (doc/document/import/include/schema/xquery modülü/transform stylesheet-location…).</summary>
    public const string ResourceDenied = "SandboxResourceDenied";

    /// <summary><c>unparsed-text*</c> / <c>json-doc</c>.</summary>
    public const string UnparsedTextDenied = "SandboxUnparsedTextDenied";

    /// <summary><c>collection()</c> / <c>uri-collection()</c>.</summary>
    public const string CollectionDenied = "SandboxCollectionDenied";

    /// <summary><c>xsl:result-document</c>.</summary>
    public const string ResultDocumentDenied = "SandboxResultDocumentDenied";

    /// <summary>İzin listesi dışı collation URI'si (statik ya da dinamik).</summary>
    public const string CollationDenied = "SandboxCollationDenied";

    /// <summary>Stylesheet'te DOCTYPE/DTD (derleme öncesi .NET okumasında reddedilir).</summary>
    public const string StylesheetDtd = "SandboxStylesheetDtd";

    /// <summary>Statik ifade (use-when, gölge <c>_*</c>, <c>static="yes"</c>) — derleme öncesi .NET okumasında reddedilir.</summary>
    public const string StylesheetStaticExpression = "SandboxStylesheetStaticExpression";

    /// <summary>Saxon'un kendi protokol/özellik reddi (ALLOWED_PROTOCOLS, dinamik değerlendirme kapalı vb.).</summary>
    public const string SaxonPolicy = "SandboxSaxonPolicy";
}
