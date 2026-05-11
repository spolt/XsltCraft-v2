using System.Text;

namespace XsltCraft.Application.Ai;

public enum AiIntent
{
    /// <summary>Selam/teşekkür/kim sin — kod bağlamı gereksiz, minimum prompt.</summary>
    Smalltalk,

    /// <summary>"XSLT nedir", "XPath nasıl çalışır" — alan bilgisi sorusu ama kullanıcı kodu okumaya gerek yok.</summary>
    General,

    /// <summary>Varsayılan: kullanıcı dosyasıyla ilgili soru — tam bağlam paketi gönderilir.</summary>
    Code,
}

/// <summary>
/// Niyet sınıflandırma — Assistant modunda prompt boyutunu kısaltmak için.
/// Refactor task'ları her zaman Code; mesajda kod referansı varsa Code.
/// </summary>
public static class IntentClassifier
{
    public static AiIntent Classify(AiRequest req)
    {
        // Refactor her zaman kod bağlamı gerektirir.
        if (req.Task == AiTaskKind.RefactorSelection) return AiIntent.Code;

        // Kullanıcı XML/XSLT seçimi yaptıysa açıkça koda referans veriyor.
        if (!string.IsNullOrWhiteSpace(req.Selection) ||
            !string.IsNullOrWhiteSpace(req.XmlSelection))
            return AiIntent.Code;

        var raw = (req.UserRequest ?? string.Empty).Trim();
        if (raw.Length == 0) return AiIntent.Smalltalk;

        var folded = Fold(raw);

        if (HasCodeSignals(raw, folded)) return AiIntent.Code;
        if (IsSmalltalk(folded, raw)) return AiIntent.Smalltalk;
        if (IsGeneralQuestion(folded)) return AiIntent.General;

        // Belirsizlikte güvenli taraf — bağlam kırpma, kaliteyi koru.
        return AiIntent.Code;
    }

    private static readonly string[] CodeReferencePhrases =
    [
        "burada", "bu kod", "bu xslt", "bu xml", "bu satir", "su satir",
        "bu blok", "bu template", "bu sablon", "bu fonksiyon",
        "hata var", "hatasi", "neden calismi", "neden olmuyor",
        "duzelt", "refactor", "optimize",
    ];

    // Güçlü kod sinyalleri — XML/XSLT sözdiziminden parçalar. Düz konuşmada geçmez.
    // "xpath", "template" gibi alfabetik kelimeler buraya KONULMAZ; çünkü genel sorularda da geçer
    // ("XPath nasıl çalışır?"). Onlar TopicKeywords altında.
    private static readonly string[] CodeTokens =
    [
        "xsl:", "cbc:", "cac:", "n1:", "xmlns",
        "for-each", "value-of",
    ];

    private static bool HasCodeSignals(string raw, string folded)
    {
        if (raw.Contains('<') || raw.Contains('>') || raw.Contains('`')) return true;

        // CodeTokens prefix-tarzı (":" içeren) — substring match yeterli ve gerekli.
        foreach (var t in CodeTokens)
            if (folded.Contains(t, StringComparison.Ordinal)) return true;

        // CodeReferencePhrases birden fazla kelime; cümle içinde substring olarak yeterli.
        foreach (var p in CodeReferencePhrases)
            if (folded.Contains(p, StringComparison.Ordinal)) return true;

        return false;
    }

    private static readonly string[] SmalltalkPhrases =
    [
        "selam", "merhaba", "selamun aleykum", "sa ",
        "tesekkur", "tesekkurler", "sagol", "sag ol", "eyvallah",
        "kimsin", "kim sin", "adin ne", "sen nesin", "ne yapabilir",
        "naber", "nasilsin", "iyi misin",
        "hoscakal", "gorusuruz", "bay bay", "gule gule", "iyi gunler", "iyi aksamlar",
        "hi", "hello", "thanks", "thank you",
    ];

    private static bool IsSmalltalk(string folded, string raw)
    {
        // Çok kısa mesajlar (selam, sa, hi gibi) — uzun bir teknik soru smalltalk değildir.
        if (raw.Length > 50) return false;

        // Kelime sınırı kontrolü zorunlu: aksi halde "sehir" → "hi" yanlış eşleşmesi olur.
        foreach (var phrase in SmalltalkPhrases)
            if (ContainsAsWord(folded, phrase)) return true;

        return false;
    }

    /// <summary>
    /// phrase'i text içinde TAM SÖZCÜK olarak arar — etrafında harf/rakam olmamalı.
    /// "hi" → "sehir" eşleşmez; "iyi günler" → "çok iyi günler" eşleşir.
    /// </summary>
    private static bool ContainsAsWord(string text, string phrase)
    {
        int idx = 0;
        while ((idx = text.IndexOf(phrase, idx, StringComparison.Ordinal)) >= 0)
        {
            bool leftOk = idx == 0 || !IsWordChar(text[idx - 1]);
            int end = idx + phrase.Length;
            bool rightOk = end == text.Length || !IsWordChar(text[end]);
            if (leftOk && rightOk) return true;
            idx++;
        }
        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static readonly string[] QuestionMarkers =
    [
        "nedir", "nasil", "neden", "niye", "ne zaman", "kim ",
        " mi", " mu", " mı", " mü", " mi?", " mu?",
        "?",
    ];

    private static readonly string[] TopicKeywords =
    [
        "xslt", "xpath", "xml", "ubl", "saxon", "xsd",
        "fatura", "earsiv", "e-fatura", "e fatura", "e-arsiv",
        "namespace", "transform", "schematron",
    ];

    private static bool IsGeneralQuestion(string folded)
    {
        bool hasQuestion = false;
        foreach (var m in QuestionMarkers)
            if (folded.Contains(m, StringComparison.Ordinal)) { hasQuestion = true; break; }
        if (!hasQuestion) return false;

        foreach (var k in TopicKeywords)
            if (folded.Contains(k, StringComparison.Ordinal)) return true;

        return false;
    }

    // PatternSelector'la aynı folding — tutarlı davranış için.
    private static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
        {
            sb.Append(c switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ç' => 'c',
                'ğ' => 'g',
                'ü' => 'u',
                'ö' => 'o',
                _ => c,
            });
        }
        return sb.ToString();
    }
}
