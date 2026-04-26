using System.Text;
using System.Text.RegularExpressions;

namespace XsltCraft.Application.Ai;

public record ProviderMessage(string Role, string Content);

public static class PromptTemplates
{
    private const string SystemRules =
        "You are a senior XML, XPath and XSLT engineer.\n" +
        "Your job is to:\n" +
        "- Analyze existing XSLT and XML together.\n" +
        "- Understand transformation logic.\n" +
        "- Modify the XSLT safely without breaking existing behavior.\n" +
        "- Saxon HE 10.9.0 (XSLT 2.0, XPath 2.0) kullanılıyor.\n" +
        "- UBL-TR e-Fatura/e-Arşiv şablonları üzerinde çalışıyorsun.\n" +
        "- Cevapları Türkçe ver.\n\n" +
        "UBL-TR namespace tabanın:\n" +
        "- cbc = CommonBasicComponents (ID, IssueDate, Note, ...)\n" +
        "- cac = CommonAggregateComponents (AccountingSupplierParty, InvoiceLine, ...)\n" +
        "- ext = CommonExtensionComponents (UBLExtensions)\n" +
        "- Tipik kök: <Invoice>, <CreditNote>, <DespatchAdvice>\n" +
        "- Satırlar: cac:InvoiceLine içinde cbc:ID, cbc:InvoicedQuantity, cac:Item, cac:Price, cac:TaxTotal\n" +
        "- Header: cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name\n" +
        "- Notlar: cbc:Note (Invoice altında veya InvoiceLine altında)\n\n" +
        "Rules:\n" +
        "- NEVER rewrite from scratch unless explicitly asked.\n" +
        "- ONLY change what is necessary.\n" +
        "- Preserve structure and templates.\n" +
        "- Explain what you changed and why.\n" +
        "When a knowledge base is provided:\n" +
        "- Prefer existing patterns\n" +
        "- Follow project conventions strictly\n" +
        "Görevin:\n" +
        "- Kullanıcının doğal dil talebini al.\n" +
        "- Mevcut XSLT'yi analiz edip hangi template/match'in etkileneceğini bul.\n" +
        "- ÖNCE planı yaz: hangi template'i, hangi satırı değiştireceğini söyle.\n" +
        "- SONRA ```xslt ... ``` bloğu ile örnek kodu ver — yapıştırılabilir parça.\n" +
        "- SONRA nereye/nasıl uygulanacağını anlat.\n\n" +
        "Güvenlik: document(), enableScript, external DTD, dış URI önerme.\n" +
        "Belirsizse varsayım yapma — eksik bilgi iste.";

    private const string CommonConstraints =
        "- Sen plan-only modundasın: kullanıcı kodu kendisi yapıştıracak.\n" +
        "- Cevabın yapısı: 1) Plan (1-3 cümle), 2) ```xslt kod```, 3) Uygulama yönergesi (1-2 cümle).\n" +
        "- Eğer talep çok küçükse (tek satır silme) sadece XPath/match göster, kod bloğu opsiyonel.\n" +
        "- UBL-TR namespace'leri: cbc, cac, ext — tanımlamadan kullanma.\n" +
        "- Kod bloğu dışındaki açıklama kısa olsun (<150 kelime).";

    private const int AssistantXsltLimitChars = 16_000;
    private const int ContextSoftLimitChars = 24_000;
    private const int MaxHistoryPairs = 10;

    private static readonly Regex VersionRe = new(
        @"\bversion=[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NsRe = new(
        @"xmlns:(\w+)=[""']([^""']+)[""']",
        RegexOptions.Compiled);

    private static string DetectXsltVersion(string? xslt)
    {
        if (string.IsNullOrEmpty(xslt)) return "2.0";
        var m = VersionRe.Match(xslt);
        return m.Success ? m.Groups[1].Value : "2.0";
    }

    private static string? BuildProjectContext(string? xslt)
    {
        var version = DetectXsltVersion(xslt);
        var sb = new StringBuilder();
        sb.Append($"XSLT Versiyonu: {version}\n");
        sb.Append("UBL Versiyonu: 2.1");

        if (!string.IsNullOrEmpty(xslt))
        {
            var head = xslt.Length > 2000 ? xslt[..2000] : xslt;
            var nsList = new List<string>();
            foreach (Match m in NsRe.Matches(head))
                nsList.Add($"xmlns:{m.Groups[1].Value}=\"{m.Groups[2].Value}\"");
            if (nsList.Count > 0)
            {
                sb.Append('\n');
                sb.Append("Namespace bildirimleri: ").Append(string.Join(", ", nsList));
            }
        }

        var result = sb.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>
    /// Tek-turn prompts (refactor-selection).
    /// </summary>
    public static string Build(AiRequest req)
    {
        var outputFormat = req.Task switch
        {
            AiTaskKind.RefactorSelection => "Verilen seçimi refactor et. Önce kısa bir özet, sonra ```xslt ... ``` bloğu içinde TAM yeni hâli (before değil after) ver.",
            _ => "Açık ve özlü cevap ver.",
        };

        var sb = new StringBuilder(4096);
        sb.Append("<system_rules>\n").Append(SystemRules).Append("\n</system_rules>\n\n");
        sb.Append("<output_format>\n").Append(outputFormat).Append("\n</output_format>\n\n");
        sb.Append("<constraints>\n").Append(CommonConstraints).Append("\n</constraints>\n\n");

        var projectCtx = BuildProjectContext(req.UserXslt);
        if (projectCtx != null)
            sb.Append("<project_context>\n").Append(projectCtx).Append("\n</project_context>\n\n");

        var (xml, xslt) = ClampContext(req.UserXml, req.UserXslt);

        if (!string.IsNullOrWhiteSpace(xml))
            sb.Append("<user_xml>\n").Append(xml).Append("\n</user_xml>\n\n");
        if (!string.IsNullOrWhiteSpace(xslt))
            sb.Append("<user_xslt>\n").Append(xslt).Append("\n</user_xslt>\n\n");
        if (!string.IsNullOrWhiteSpace(req.Selection))
            sb.Append("<user_selection>\n").Append(req.Selection).Append("\n</user_selection>\n\n");

        sb.Append("<user_request>\n").Append(req.UserRequest ?? string.Empty).Append("\n</user_request>");
        return sb.ToString();
    }

    /// <summary>
    /// Multi-turn assistant: returns ordered messages list.
    /// Index 0 is always the "system" message (role = "system").
    /// Subsequent messages alternate user/assistant.
    /// Last message is always role = "user" (new user message).
    /// </summary>
    public static IReadOnlyList<ProviderMessage> BuildAssistant(AiRequest req)
    {
        var messages = new List<ProviderMessage>();

        // ── System message ─────────────────────────────────────────────────────
        var systemSb = new StringBuilder();
        systemSb.Append(SystemRules).Append("\n\n");
        systemSb.Append(CommonConstraints);

        var projectCtx = BuildProjectContext(req.UserXslt);
        if (projectCtx != null)
            systemSb.Append("\n\n<project_context>\n").Append(projectCtx).Append("\n</project_context>");

        messages.Add(new ProviderMessage("system", systemSb.ToString()));

        // ── Context message (user): fresh XSLT + XML each turn ─────────────────
        var ctxSb = new StringBuilder();
        var xsltClipped = Clip(req.UserXslt, AssistantXsltLimitChars);
        var xmlClipped = Clip(req.UserXml, ContextSoftLimitChars - AssistantXsltLimitChars);

        if (!string.IsNullOrWhiteSpace(xsltClipped))
            ctxSb.Append("<user_xslt>\n").Append(xsltClipped).Append("\n</user_xslt>\n");
        if (!string.IsNullOrWhiteSpace(req.XmlSelection))
            ctxSb.Append("<user_xml_selection>\n").Append(req.XmlSelection).Append("\n</user_xml_selection>\n");
        else if (!string.IsNullOrWhiteSpace(xmlClipped))
            ctxSb.Append("<user_xml>\n").Append(xmlClipped).Append("\n</user_xml>\n");

        if (ctxSb.Length > 0)
            messages.Add(new ProviderMessage("user", ctxSb.ToString().TrimEnd()));

        // ── History (trim if too long) ─────────────────────────────────────────
        var history = req.History ?? [];
        // History çiftler halinde — user+assistant. Son MaxHistoryPairs çifti al.
        if (history.Count > MaxHistoryPairs * 2)
            history = history[^(MaxHistoryPairs * 2)..];

        // Context mesajı için dummy assistant ack (gerekirse)
        if (ctxSb.Length > 0 && history.Count > 0)
            messages.Add(new ProviderMessage("assistant", "Anladım, XSLT ve XML bağlamını aldım."));

        foreach (var h in history)
            messages.Add(new ProviderMessage(h.Role, h.Content));

        // ── New user message ───────────────────────────────────────────────────
        messages.Add(new ProviderMessage("user", req.UserRequest ?? string.Empty));

        return messages;
    }

    private static (string? xml, string? xslt) ClampContext(string? xml, string? xslt)
    {
        var xmlLen = xml?.Length ?? 0;
        var xsltLen = xslt?.Length ?? 0;
        var total = xmlLen + xsltLen;
        if (total <= ContextSoftLimitChars) return (xml, xslt);

        var perBlock = ContextSoftLimitChars / 2;
        return (Clip(xml, perBlock), Clip(xslt, perBlock));
    }

    private static string? Clip(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        var head = max / 2;
        var tail = max - head;
        return s[..head] + "\n... [kırpıldı] ...\n" + s[^tail..];
    }
}
