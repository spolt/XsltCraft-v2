using net.sf.saxon.trans;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// Sandbox katmanlarının (resolver'lar, collation) fırlattığı hata. Saxon bunu kendi hatasıyla sarabilir
/// ya da yalnız mesajını kopyalayabilir; bu yüzden mesaj <see cref="Marker"/> önekini taşır ve
/// <c>SaxonDiagnostics</c> hem tür (cause zinciri) hem önek üzerinden tanır.
/// </summary>
public sealed class SandboxViolationException : XPathException
{
    internal const string Marker = "[xsltcraft-sandbox:";

    public SandboxViolationException(string code, string message)
        : base(Marker + code + "] " + message)
    {
        Code = code;
        UserMessage = message;
    }

    /// <summary><see cref="SandboxCodes"/> değeri.</summary>
    public string Code { get; }

    /// <summary>Önek'siz, kullanıcıya gösterilebilir Türkçe mesaj (URI içermez).</summary>
    public string UserMessage { get; }
}
