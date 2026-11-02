using System.Text.RegularExpressions;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Source;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>
/// Yuva işaretlerinin XSLT'deki biçimi — tek kaynak. Eklenen işaret boş ve stilsizdir; görsel yokken yer
/// kaplamaz:
/// <c>&lt;div data-xc-slot="qr-below" data-xc-layout="block-clear" data-xc-inserted="1"&gt;&lt;/div&gt;</c>.
/// Mevcut tutucuya yalnız <c>data-xc-slot</c> niteliği eklenir; yerini kullanıcının görselinin alacağı temadaki statik
/// görsele <c>data-xc-slot</c> + <c>data-xc-placeholder="logo|signature"</c> (+ kullanıcı kaldırabiliyorsa
/// <c>data-xc-removable="1"</c>). Nitelik sırası sabittir (temizleme
/// regex'i buna dayanır).
/// </summary>
internal static class ThemeSlotMarkers
{
    public const string SlotAttribute = "data-xc-slot";
    public const string LayoutAttribute = "data-xc-layout";
    public const string OccupiedAttribute = "data-xc-occupied";
    public const string PlaceholderAttribute = "data-xc-placeholder";
    public const string RemovableAttribute = "data-xc-removable";

    /// <summary>Kararlı yuva kimliği. <c>\z</c>: <c>$</c> sondaki <c>\n</c>'i de kabul ederdi.</summary>
    public static readonly Regex SlotIdPattern = new(@"^[a-z][a-z0-9-]{1,40}\z", RegexOptions.CultureInvariant);

    private static readonly Regex InsertedDiv = new(
        @"<div data-xc-slot=""[a-z0-9-]+"" data-xc-layout=""[a-z-]+"" data-xc-inserted=""1""\s*(?:/>|>(?:<[A-Za-z_][\w.-]*:comment\s*/>|<!---->)?</div>)",
        RegexOptions.CultureInvariant);

    private static readonly Regex SlotAttr = new(@" data-xc-slot=""[a-z0-9-]+""", RegexOptions.CultureInvariant);

    private static readonly Regex PlaceholderAttr = new(@" data-xc-placeholder=""(?:logo|signature)""(?: data-xc-removable=""1"")?", RegexOptions.CultureInvariant);

    private static readonly Regex SlotIds = new(@"data-xc-slot=""([a-z][a-z0-9-]{1,40})""", RegexOptions.CultureInvariant);

    /// <summary>Önceki analizden kalan işaretleri siler (idempotent). <c>data-xc-occupied</c> korunur.</summary>
    public static string Clean(string source) =>
        PlaceholderAttr.Replace(SlotAttr.Replace(InsertedDiv.Replace(source, string.Empty), string.Empty), string.Empty);

    /// <summary>Render edilmiş HTML'den işaretleri siler — "işaretler çıkınca çıktı aynı" denetimi için.</summary>
    public static string StripFromHtml(string html) => Clean(html);

    /// <summary>Metindeki tüm yuva kimlikleri (XSLT ya da render edilmiş HTML).</summary>
    public static IReadOnlyCollection<string> SlotIdsIn(string text) =>
        SlotIds.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    public static int CountOccurrences(string html, string slotId) =>
        Regex.Matches(html, $@"data-xc-slot=""{Regex.Escape(slotId)}""", RegexOptions.CultureInvariant).Count;

    public static string LayoutValue(SlotLayout layout) => layout switch
    {
        SlotLayout.BlockClear => "block-clear",
        SlotLayout.FloatLeft => "float-left",
        _ => "block",
    };

    /// <summary>İşaretleri kaynağa yazar: yeni noktalara boş div, benimsenen tutuculara ve temadaki görsellere nitelik.
    /// Bir elemana ikinci kez <c>data-xc-slot</c> yazılacaksa (yinelenen nitelik temayı derlenemez yapar)
    /// <see cref="InvalidOperationException"/> fırlatır.</summary>
    public static string Write(ThemeDocument d, IEnumerable<ReducedSlot> slots)
    {
        var edits = new SourceEdits();
        var marked = new HashSet<XElement>(ReferenceEqualityComparer.Instance);
        void Claim(XElement e, string slotId)
        {
            if (e.Attribute(SlotAttribute) is not null || !marked.Add(e))
                throw new InvalidOperationException($"{slotId} yuvası zaten işaretli bir elemana yazılamaz (yinelenen {SlotAttribute}).");
        }

        foreach (var slot in slots)
        {
            var c = slot.Candidate;
            if (c.Placeholders is { } placeholders)
            {
                var kind = c.PlaceholderKind == ThemeAssetKind.Signature ? "signature" : "logo";
                foreach (var img in placeholders)
                {
                    Claim(img, slot.Id);
                    var removable = c.RemovablePlaceholders?.Contains(img) == true ? $@" {RemovableAttribute}=""1""" : string.Empty;
                    edits.Insert(NameEnd(d.Map.Source, d.Map.Span(img).Start),
                        $@" {SlotAttribute}=""{slot.Id}"" {PlaceholderAttribute}=""{kind}""{removable}");
                }
            }
            else if (c.AdoptedHolder is not null)
            {
                Claim(c.AdoptedHolder, slot.Id);
                var span = d.Map.Span(c.AdoptedHolder);
                edits.Insert(NameEnd(d.Map.Source, span.Start), $@" {SlotAttribute}=""{slot.Id}""");
            }
            else if (c.Point is not null)
            {
                var inner = d.MarkerStyle == MarkerStyle.WithComment ? $"<{d.XslPrefix}:comment/>" : string.Empty;
                edits.Insert(c.Point.Offset,
                    $@"<div {SlotAttribute}=""{slot.Id}"" {LayoutAttribute}=""{LayoutValue(c.Layout)}"" data-xc-inserted=""1"">{inner}</div>");
            }
        }
        return edits.Apply(d.Map.Source);
    }

    // Açılış etiketindeki eleman adının bittiği ofset ('<' + qname).
    private static int NameEnd(string source, int start)
    {
        var i = start + 1;
        while (i < source.Length && !char.IsWhiteSpace(source[i]) && source[i] is not ('>' or '/'))
            i++;
        return i;
    }
}
