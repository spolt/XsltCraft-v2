using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XsltCraft.Application.Ai;

public record ProviderMessage(string Role, string Content);

public static class PromptTemplates
{
    private const int AssistantXsltLimitChars = 16_000;
    private const int ContextSoftLimitChars = 24_000;
    private const int MaxHistoryPairs = 10;

    private static readonly Regex VersionRe = new(
        @"\bversion=[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NsRe = new(
        @"xmlns:(\w+)=[""']([^""']+)[""']",
        RegexOptions.Compiled);

    // ── Core builder ──────────────────────────────────────────────────────────

    internal static IReadOnlyList<ProviderMessage> BuildMessages(AiRequest req, AiMode mode)
    {
        // Refactor modunda her zaman tam paket; Assistant modunda niyete göre kısalt.
        var intent = mode == AiMode.Refactor ? AiIntent.Code : IntentClassifier.Classify(req);
        Debug.WriteLine($"[IntentClassifier] intent: {intent}");

        var patterns = intent == AiIntent.Smalltalk
            ? []
            : PatternSelector.Select(req);
        Debug.WriteLine($"[PatternSelector] selected: [{string.Join(", ", patterns.Select(p => p.Id))}]");

        // System mesajı SABİT → DEĞİŞKEN sırasıyla kurulur ki Ollama prefix KV-cache'i
        // daha sık tutsun:
        //   1) Identity (gömülü, hiç değişmez)
        //   2) Constraints (gömülü, hiç değişmez)  ← önceden patterns'tan sonraydı
        //   3) patterns (soruya göre değişir)
        //   4) project_context (XSLT'ye göre değişir)
        var systemSb = new StringBuilder();
        systemSb.Append(PromptRegistry.Identity);

        if (intent != AiIntent.Smalltalk)
        {
            systemSb.Append("\n\n").Append(PromptRegistry.Constraints);

            foreach (var p in patterns)
                systemSb.Append("\n\n").Append(p.Content);

            var projectCtx = BuildProjectContext(req.UserXslt);
            if (projectCtx != null)
                systemSb.Append("\n\n<project_context>\n").Append(projectCtx).Append("\n</project_context>");
        }

        var messages = new List<ProviderMessage> { new("system", systemSb.ToString()) };

        if (mode == AiMode.Refactor)
        {
            var outputFormat = req.Task switch
            {
                AiTaskKind.RefactorSelection =>
                    "Verilen seçimi refactor et. Önce kısa bir özet, sonra ```xslt ... ``` bloğu içinde TAM yeni hâli (before değil after) ver.",
                _ => "Açık ve özlü cevap ver.",
            };

            var userSb = new StringBuilder();
            userSb.Append("<output_format>\n").Append(outputFormat).Append("\n</output_format>\n\n");

            var (xml, xslt) = ClampContext(req.UserXml, req.UserXslt);
            if (!string.IsNullOrWhiteSpace(xml))
                userSb.Append("<user_xml>\n").Append(xml).Append("\n</user_xml>\n\n");
            if (!string.IsNullOrWhiteSpace(xslt))
                userSb.Append("<user_xslt>\n").Append(xslt).Append("\n</user_xslt>\n\n");
            if (!string.IsNullOrWhiteSpace(req.Selection))
                userSb.Append("<user_selection>\n").Append(req.Selection).Append("\n</user_selection>\n\n");

            userSb.Append("<user_request>\n").Append(req.UserRequest ?? string.Empty).Append("\n</user_request>");
            messages.Add(new("user", userSb.ToString()));
        }
        else // Assistant
        {
            // Bağlam paketi yalnızca Code niyetinde; smalltalk/general'da XSLT/XML gönderilmez.
            var ctxSb = new StringBuilder();
            if (intent == AiIntent.Code)
            {
                // Büyük XSLT'ler önce yapısal özete indirilir (prefill maliyetini azaltır);
                // sonra Clip güvenlik ağı olarak kalır.
                var xsltSummarized = XsltSummarizer.Compose(req.UserXslt, req.Selection);
                var xsltClipped = Clip(xsltSummarized, AssistantXsltLimitChars);
                var xmlClipped = Clip(req.UserXml, ContextSoftLimitChars - AssistantXsltLimitChars);

                if (!string.IsNullOrWhiteSpace(xsltClipped))
                    ctxSb.Append("<user_xslt>\n").Append(xsltClipped).Append("\n</user_xslt>\n");
                if (!string.IsNullOrWhiteSpace(req.XmlSelection))
                    ctxSb.Append("<user_xml_selection>\n").Append(req.XmlSelection).Append("\n</user_xml_selection>\n");
                else if (!string.IsNullOrWhiteSpace(xmlClipped))
                    ctxSb.Append("<user_xml>\n").Append(xmlClipped).Append("\n</user_xml>\n");

                if (ctxSb.Length > 0)
                    messages.Add(new("user", ctxSb.ToString().TrimEnd()));
            }

            // History — niyete göre limit: smalltalk 2 çift, general 5, code 10.
            var historyLimit = intent switch
            {
                AiIntent.Smalltalk => 2,
                AiIntent.General => 5,
                _ => MaxHistoryPairs,
            };

            var history = req.History ?? [];
            if (history.Count > historyLimit * 2)
                history = history[^(historyLimit * 2)..];

            if (ctxSb.Length > 0 && history.Count > 0)
                messages.Add(new("assistant", "Anladım, XSLT ve XML bağlamını aldım."));

            foreach (var h in history)
                messages.Add(new(h.Role, h.Content));

            messages.Add(new("user", req.UserRequest ?? string.Empty));
        }

        return messages;
    }

    // ── Public API (geri uyum) ────────────────────────────────────────────────

    /// <summary>Tek-turn prompt (refactor-selection). Provider'lara tek string olarak iletilir.</summary>
    public static string Build(AiRequest req)
    {
        var messages = BuildMessages(req, AiMode.Refactor);
        var system = messages.First(m => m.Role == "system");
        var user = messages.Last(m => m.Role == "user");
        return new StringBuilder()
            .Append("<system_rules>\n").Append(system.Content).Append("\n</system_rules>\n\n")
            .Append(user.Content)
            .ToString();
    }

    /// <summary>
    /// Multi-turn assistant: dönen listenin ilk elemanı "system" mesajıdır.
    /// Sonraki mesajlar user/assistant olarak sıralanır; son mesaj her zaman "user"dır.
    /// </summary>
    public static IReadOnlyList<ProviderMessage> BuildAssistant(AiRequest req)
        => BuildMessages(req, AiMode.Assistant);

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

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
                sb.Append('\n').Append("Namespace bildirimleri: ").Append(string.Join(", ", nsList));
        }

        var result = sb.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static (string? xml, string? xslt) ClampContext(string? xml, string? xslt)
    {
        var total = (xml?.Length ?? 0) + (xslt?.Length ?? 0);
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
