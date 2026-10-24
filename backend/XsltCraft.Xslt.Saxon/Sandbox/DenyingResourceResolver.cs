using javax.xml.transform;
using net.sf.saxon.lib;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// Tüm dış kaynak isteklerini reddeder (Configuration, XsltCompiler ve transformer düzeyinde kurulur).
/// <c>null</c> DÖNMEZ: Saxon'da null "varsayılan çözümleyiciye düş" demektir (spike tuzağı).
/// URI mesaja konmaz (yol/iç ağ adresi sızmasın).
/// </summary>
internal sealed class DenyingResourceResolver : ResourceResolver
{
    public static readonly DenyingResourceResolver Instance = new();

    public Source resolve(ResourceRequest request) =>
        throw new SandboxViolationException(SandboxCodes.ResourceDenied, "Dış kaynak erişimi yasak (doc/document/import/include vb.).");
}
