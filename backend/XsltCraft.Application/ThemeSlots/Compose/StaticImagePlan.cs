using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.ThemeSlots.Source;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>
/// Statik görsel isteklerini kaynak düzenlemelerine çevirir. Yalnız literal, alt elemansız <c>img</c>'ler ve literal
/// yuva kutuları düzenlenir; taşınabilirlik <see cref="StaticImageSources"/>'tan gelir. Görsel bloğunun içi ve XSLT
/// talimatları değişmez. Her görsel bir kez işlenir; aynı elemana ikinci düzenleme çakışma uyarısıyla atlanır (görsel
/// yerinde kalır). Taşınan ya da silinen görselin kendi yuvası başka elemanda yoksa yerinde boş bir eklenmiş işaret
/// kutusu kalır (yuva kimliği korunur; <see cref="ThemeSlotMarkers.Clean"/> onu siler); başka bir görsel o yuvaya
/// taşınırsa kutuyu doldurur. Taşınan görsel kendi yuva işaretini bırakır, rol işaretini taşır.
/// </summary>
internal sealed class StaticImagePlan(ThemeDocument document, IReadOnlyList<BlockRange> blocks, IReadOnlyList<XElement> images,
    IReadOnlySet<ThemeAssetKind> ignoredKinds, StaticImageWarnings warnings, long byteBudget)
{
    /// <summary>Bir yuva grubunda görselin yerini alabileceği en fazla <c>img</c>.</summary>
    public const int MaxGroupImages = 4;

    private const string InsertedAttribute = "data-xc-inserted";

    private readonly SourceEdits _edits = new();
    // true: başka bir görsel bu görselin kutusuna yerleşti (replace); false: istekteki görselin kendisi.
    private readonly Dictionary<XElement, bool> _claimed = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<XElement, List<string>> _boxes = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _filledBoxSlots = new(StringComparer.Ordinal);
    // Boşalan görsel yuvaları: görselin (ya da sarmalayıcısının) aralığı boş kutuya ya da gelen görsellere çevrilir.
    private readonly Dictionary<string, VacatedBox> _vacated = new(StringComparer.Ordinal);

    private XsltSourceMap Map => document.Map;

    private string Source => Map.Source;

    /// <summary>Bir yuva grubundaki ek görsellerle metne eklenen görsel kaynağı (karakter).</summary>
    public int AddedBytes { get; private set; }

    public bool Changed => _edits.Count > 0 || _boxes.Count > 0 || _vacated.Count > 0;

    /// <summary>İçine statik görsel yerleşen kutu yuvaları (eklenen işaret kutuları dahil).</summary>
    public IReadOnlyCollection<string> FilledBoxSlots => _filledBoxSlots;

    /// <summary>Başka bir isteğin boşalttığı yuvaya giden istekler sona alınır: önce görsel ayrılır, sonra yuvası dolar.</summary>
    public IEnumerable<StaticImageInput> InApplyOrder(IReadOnlyList<StaticImageInput> requests)
    {
        var vacating = requests
            .Where(r => r.Index >= 0 && r.Index < images.Count)
            .Select(r => (Request: r, Own: OwnSlot(images[r.Index])))
            .Where(x => x.Own is not null && (x.Request.Remove || (x.Request.Slot is { } s && s != x.Own)))
            .Select(x => x.Own!)
            .ToHashSet(StringComparer.Ordinal);
        return requests.OrderBy(r => r.Slot is { } s && vacating.Contains(s) ? 1 : 0);
    }

    public void Apply(StaticImageInput r)
    {
        if (r.Index >= images.Count)
        {
            warnings.Add(StaticImageWarnings.NotFound, r.Index, null);
            return;
        }
        var img = images[r.Index];
        var kind = KindOf(img);
        if (!IsEditableImage(img))
        {
            warnings.Add(StaticImageWarnings.NotEditable, r.Index, kind);
            return;
        }
        if (kind is { } k && ignoredKinds.Contains(k))
        {
            warnings.Add(StaticImageWarnings.Ignored, r.Index, kind);
            return;
        }
        if (!_claimed.TryAdd(img, false))
        {
            warnings.Add(StaticImageWarnings.Conflict, r.Index, kind);
            return;
        }

        var span = Map.Span(img);
        if (r.Remove)
        {
            RemoveImage(img);
            return;
        }

        var tag = Source[span.Start..span.StartTagEnd];
        var rest = Source[span.StartTagEnd..span.End];
        var styled = StaticImageMarkup.StyleImage(tag, r.Width, r.Height, r.Alignment);
        // Yuva kimliği eski yerde kalır (boş kutu); taşınan görsel yalnız rol işaretini taşır.
        var movedTag = StartTagText.Remove(styled, ThemeSlotMarkers.SlotAttribute);
        var src = StartTagText.Value(tag, "src");
        var movable = StaticImageSources.IsAllowed(src) && !StaticImageSources.HasDynamicAttribute(img);

        var moved = false;
        if (r.Slot is { } slot && !IsAt(img, slot))
        {
            if (!movable)
                warnings.Add(StaticImageWarnings.NotEditable, r.Index, kind);
            else if (Targets(slot) is not { Count: > 0 } targets)
                warnings.Add(StaticImageWarnings.SlotNotFound, r.Index, kind);
            else if (Place(slot, targets, movedTag + rest, new Placement(src!, KindName(kind), r)) is { } code)
                warnings.Add(code, r.Index, kind);
            else
                moved = true;
        }
        if (moved)
            RemoveImage(img);
        else if (!ReferenceEquals(styled, tag))
            _edits.Replace(span.Start, span.StartTagEnd, styled);
    }

    public string Render()
    {
        foreach (var (box, parts) in _boxes)
        {
            var span = Map.Span(box);
            var content = string.Concat(parts);
            if (box.Attribute(InsertedAttribute) is not null)
                _edits.Replace(span.Start, span.End, content); // eklenen işaret kutusunun yerini sarmalayıcı alır
            else if (span.SelfClosing)
                _edits.Replace(span.End - 2, span.End, $">{content}</{QualifiedName(box)}>");
            else
                _edits.Insert(span.EndTagStart, content);
        }
        foreach (var (slot, box) in _vacated)
            _edits.Replace(box.Start, box.End,
                box.Parts.Count == 0 ? ThemeSlotMarkers.InsertedMarker(document, slot, SlotLayout.Block) : string.Concat(box.Parts));
        return _edits.Apply(Source);
    }

    private sealed record Placement(string Src, string? Kind, StaticImageInput Request);

    private sealed record VacatedBox(int Start, int End, string Prefix)
    {
        public List<string> Parts { get; } = [];
    }

    // ThemeAssetRuntime.js place(): görsel hedefin yerini alır; aynı hedefe ikinci yerleşim ardına eklenir; kutuya içine.
    // Eklenen işaret kutusunun yerini sarmalayıcı alır; ilk sarmalayıcı yuva işaretini taşır (yuva dolu kalır).
    // Boşalan görsel yuvasında görsel, boş kutunun yerine (ilk sarmalayıcı yuva işaretiyle) yazılır.
    // Dönüş: null → yerleşti; aksi hâlde uyarı kodu.
    private string? Place(string slot, List<XElement> targets, string markup, Placement p)
    {
        if (_vacated.TryGetValue(slot, out var vacated))
        {
            vacated.Parts.Add(StaticImageMarkup.Wrapper(vacated.Prefix, null, p.Request.Alignment ?? ThemeAlignment.Left, markup,
                vacated.Parts.Count == 0 ? slot : null));
            _filledBoxSlots.Add(slot);
            return null;
        }

        var first = targets[0];
        if (IsLiteral(first, "img") || first.Attribute(ThemeSlotMarkers.PlaceholderAttribute) is not null)
        {
            var group = targets.Where(t => IsLiteral(t, "img")).ToList();
            if (group.Count == 0 || !group.All(IsEditableImage) || group.Any(g => _claimed.TryGetValue(g, out var replaced) && !replaced))
                return StaticImageWarnings.Conflict;
            if (group.Count > MaxGroupImages)
                return StaticImageWarnings.TooLarge;
            if (group.Any(_claimed.ContainsKey))
            {
                var last = group[^1];
                _edits.Insert(Map.Span(last).End, StaticImageMarkup.Wrapper(
                    StaticImageMarkup.PrefixOf(QualifiedName(last)), "block-clear", p.Request.Alignment ?? ThemeAlignment.Left, markup));
                return null;
            }
            if (!Reserve((long)p.Src.Length * (group.Count - 1)))
                return StaticImageWarnings.TooLarge;
            foreach (var target in group)
            {
                var span = Map.Span(target);
                _edits.Replace(span.Start, span.StartTagEnd, StaticImageMarkup.ReplaceInto(Source[span.Start..span.StartTagEnd], p.Src, p.Kind,
                    p.Request.Width, p.Request.Height, p.Request.Alignment));
                _claimed[target] = true;
            }
            return null;
        }

        if (!_boxes.TryGetValue(first, out var parts))
            _boxes[first] = parts = [];
        var boxSlot = (string?)first.Attribute(ThemeSlotMarkers.SlotAttribute);
        var carriedSlot = parts.Count == 0 && first.Attribute(InsertedAttribute) is not null ? boxSlot : null;
        parts.Add(StaticImageMarkup.Wrapper(StaticImageMarkup.PrefixOf(QualifiedName(first)),
            (string?)first.Attribute(ThemeSlotMarkers.LayoutAttribute), p.Request.Alignment ?? ThemeAlignment.Left, markup, carriedSlot));
        // Çalışma betiği dolu kutuyu boşaltırdı: aynı birleştirmedeki yeni görsel bu yuvaya yerleşmez.
        if (boxSlot is not null)
            _filledBoxSlots.Add(boxSlot);
        return null;
    }

    // Eklenecek kaynak, metnin izin verilen boyutu içinde kalıyorsa ayrılır.
    private bool Reserve(long added)
    {
        if (added == 0)
            return true;
        if (AddedBytes + added > byteBudget)
            return false;
        AddedBytes += (int)added;
        return true;
    }

    // Görsel, yalnız onu içeren statik sarmalayıcıdaysa sarmalayıcıyla birlikte silinir (boş sarmalayıcı kalmaz).
    // Görselin kendi yuvası başka elemanda yoksa aralığa boş eklenmiş kutu yazılır (Render).
    private void RemoveImage(XElement img)
    {
        var target = img.Parent is { } parent && IsLiteral(parent, "div") && parent.Attribute(StaticImageMarkup.WrapAttribute) is not null
            && parent.Nodes().All(n => ReferenceEquals(n, img) || (n is XText t && string.IsNullOrWhiteSpace(t.Value))) && !InBlock(parent)
                ? parent
                : img;
        var span = Map.Span(target);
        if (OwnSlot(img) is { } own && !_vacated.ContainsKey(own) && Targets(own) is [var only] && ReferenceEquals(only, img))
            _vacated[own] = new VacatedBox(span.Start, span.End, StaticImageMarkup.PrefixOf(QualifiedName(img)));
        else
            _edits.Replace(span.Start, span.End, string.Empty);
    }

    private static string? OwnSlot(XElement img) => (string?)img.Attribute(ThemeSlotMarkers.SlotAttribute);

    private List<XElement> Targets(string slot) =>
        Map.Document.Descendants()
            .Where(e => IsLiteral(e) && (string?)e.Attribute(ThemeSlotMarkers.SlotAttribute) == slot && !InBlock(e))
            .ToList();

    // Görsel zaten bu yuvada: kendi işareti ya da bir literal atası.
    private static bool IsAt(XElement img, string slot) =>
        img.AncestorsAndSelf().Any(e => IsLiteral(e) && (string?)e.Attribute(ThemeSlotMarkers.SlotAttribute) == slot);

    private bool IsEditableImage(XElement e) => IsLiteral(e, "img") && !e.HasElements && !InBlock(e);

    private bool InBlock(XElement e)
    {
        var start = Map.Span(e).Start;
        return blocks.Any(b => start >= b.Start && start < b.End);
    }

    private string QualifiedName(XElement e)
    {
        var span = Map.Span(e);
        return StartTagText.Parse(Source[span.Start..span.StartTagEnd])?.QualifiedName
            ?? throw new InvalidOperationException("Açılış etiketi okunamadı.");
    }

    private static ThemeAssetKind? KindOf(XElement img) => (string?)img.Attribute(ThemeSlotMarkers.PlaceholderAttribute) switch
    {
        "logo" => ThemeAssetKind.Logo,
        "signature" => ThemeAssetKind.Signature,
        _ => null,
    };

    private static string? KindName(ThemeAssetKind? kind) => kind switch
    {
        ThemeAssetKind.Logo => "logo",
        ThemeAssetKind.Signature => "signature",
        _ => null,
    };
}
