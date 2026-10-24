using System.Xml;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Motor-bağımsız, tek geçişli <see cref="XmlReader"/> dekoratörü: okuma SIRASINDA eleman derinliğini ve
/// düğüm sayısını (eleman/metin/yorum… + öznitelikler) sınırlar; ayrı bir ön tarama (çift parse) gerekmez.
/// Sınır aşılınca <see cref="XsltProcessingException"/> fırlatır (konum iç okuyucudan).
/// İç okuyucu <see cref="SecureXslt.ReaderSettings"/> ile oluşturulmalıdır (DTD yasak, resolver yok);
/// karakter sınırı iç okuyucunun <c>MaxCharactersInDocument</c> ayarıdır.
/// Kullananlar: <see cref="DotNetCompiledXslt"/> (XmlDocument yükleme), <see cref="SecureXslt.CompileWithDepthLimit"/>,
/// Saxon <c>XmlReaderTreeBridge</c>.
/// </summary>
public sealed class LimitedXmlReader : XmlReader, IXmlLineInfo, IXmlNamespaceResolver
{
    private readonly XmlReader _inner;
    private readonly int _maxDepth;
    private readonly long _maxNodes;
    private readonly XsltFailureKind _kind;
    private readonly XsltEngineKind? _engine;
    private readonly string _subject;
    private long _nodes;

    /// <param name="inner">Sarılan okuyucu (dispose sahipliği bu nesneye geçer).</param>
    /// <param name="maxDepth">En fazla eleman seviyesi (kök = 1).</param>
    /// <param name="maxNodes">En fazla düğüm (öznitelikler dahil).</param>
    /// <param name="kind">İhlalde raporlanacak hata sınıfı.</param>
    /// <param name="engine">İhlali raporlayan motor.</param>
    /// <param name="subject">Mesajdaki özne ("XML", "XSLT").</param>
    public LimitedXmlReader(XmlReader inner, int maxDepth, long maxNodes, XsltFailureKind kind,
        XsltEngineKind? engine = null, string subject = "XML")
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNodes, 1);
        _inner = inner;
        _maxDepth = maxDepth;
        _maxNodes = maxNodes;
        _kind = kind;
        _engine = engine;
        _subject = subject;
    }

    /// <summary>Şu ana dek okunan düğüm sayısı (öznitelikler dahil).</summary>
    public long NodeCount => _nodes;

    public override bool Read()
    {
        if (!_inner.Read())
            return false;

        _nodes += 1 + _inner.AttributeCount;
        if (_inner.NodeType == XmlNodeType.Element && _inner.Depth + 1 > _maxDepth)
            throw Fail($"{_subject} derinlik sınırı aşıldı (en fazla {_maxDepth}).");
        if (_nodes > _maxNodes)
            throw Fail($"{_subject} düğüm sayısı sınırı aşıldı (en fazla {_maxNodes}).");
        return true;
    }

    private XsltProcessingException Fail(string message)
    {
        var li = _inner as IXmlLineInfo;
        var line = li?.HasLineInfo() == true ? li.LineNumber : 0;
        var column = li?.HasLineInfo() == true ? li.LinePosition : 0;
        return new XsltProcessingException(new XsltDiagnostic(message, line, column, _kind, _engine));
    }

    // ---- saf yönlendirme -------------------------------------------------------------------------

    public override XmlReaderSettings? Settings => _inner.Settings;
    public override XmlNodeType NodeType => _inner.NodeType;
    public override string Name => _inner.Name;
    public override string LocalName => _inner.LocalName;
    public override string NamespaceURI => _inner.NamespaceURI;
    public override string Prefix => _inner.Prefix;
    public override bool HasValue => _inner.HasValue;
    public override string Value => _inner.Value;
    public override int Depth => _inner.Depth;
    public override string BaseURI => _inner.BaseURI;
    public override bool IsEmptyElement => _inner.IsEmptyElement;
    public override bool IsDefault => _inner.IsDefault;
    public override char QuoteChar => _inner.QuoteChar;
    public override XmlSpace XmlSpace => _inner.XmlSpace;
    public override string XmlLang => _inner.XmlLang;
    public override Type ValueType => _inner.ValueType;
    public override int AttributeCount => _inner.AttributeCount;
    public override bool EOF => _inner.EOF;
    public override ReadState ReadState => _inner.ReadState;
    public override XmlNameTable NameTable => _inner.NameTable;
    public override bool CanResolveEntity => _inner.CanResolveEntity;
    public override bool CanReadBinaryContent => false;
    public override bool CanReadValueChunk => false;

    public override string? GetAttribute(string name) => _inner.GetAttribute(name);
    public override string? GetAttribute(string name, string? namespaceURI) => _inner.GetAttribute(name, namespaceURI);
    public override string GetAttribute(int i) => _inner.GetAttribute(i);
    public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);
    public override bool MoveToAttribute(string name, string? ns) => _inner.MoveToAttribute(name, ns);
    public override void MoveToAttribute(int i) => _inner.MoveToAttribute(i);
    public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();
    public override bool MoveToNextAttribute() => _inner.MoveToNextAttribute();
    public override bool MoveToElement() => _inner.MoveToElement();
    public override bool ReadAttributeValue() => _inner.ReadAttributeValue();
    public override string? LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);
    public override void ResolveEntity() => _inner.ResolveEntity();

    public override void Close() => _inner.Close();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }

    bool IXmlLineInfo.HasLineInfo() => (_inner as IXmlLineInfo)?.HasLineInfo() == true;
    int IXmlLineInfo.LineNumber => (_inner as IXmlLineInfo)?.LineNumber ?? 0;
    int IXmlLineInfo.LinePosition => (_inner as IXmlLineInfo)?.LinePosition ?? 0;

    IDictionary<string, string> IXmlNamespaceResolver.GetNamespacesInScope(XmlNamespaceScope scope) =>
        (_inner as IXmlNamespaceResolver)?.GetNamespacesInScope(scope) ?? new Dictionary<string, string>();

    string? IXmlNamespaceResolver.LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);

    string? IXmlNamespaceResolver.LookupPrefix(string namespaceName) =>
        (_inner as IXmlNamespaceResolver)?.LookupPrefix(namespaceName);
}
