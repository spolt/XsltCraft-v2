using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Application.Validation;

namespace XsltCraft.Application.ThemeSlots.Overrides;

/// <summary>Admin'in yüklediği temada önceden gömülü görsel bloğu bulunduğunda vereceği karar.</summary>
public enum OverridesDecision { Strip, Keep }

/// <param name="Slot">v2 yuva kimliği.</param>
/// <param name="ExtraSlots">v2 ek yuvalar (<c>xs</c>).</param>
/// <param name="Keep">v2 <c>keep</c>: temadaki aynı tür görsel gizlenmez.</param>
public sealed record OverridesImageInfo(ThemeAssetKind? Kind, IReadOnlyList<string> Targets, string? DataUrl,
    int? Width, int? Height, string Alignment, bool Valid, string? Problem,
    string? Slot = null, IReadOnlyList<string>? ExtraSlots = null, bool Keep = false);

/// <summary>Bloktaki serbest görsel (<c>k:"free"</c>); geçersizse <c>DataUrl</c> null.</summary>
public sealed record OverridesFreeImageInfo(string Slot, string? DataUrl, int? Width, int? Height, string Alignment,
    bool Valid, string? Problem);

/// <param name="Present">Tam bir blok bulundu (karar gerekir).</param>
/// <param name="Keepable">"Temanın parçası olarak koru" uygulanabilir.</param>
/// <param name="Recognized">Blok içeriği tanınan biçimde (blok yoksa false).</param>
/// <param name="Hide">v2 <c>hide</c>: kullanıcının görsel eklemeden kaldırdığı türler.</param>
/// <param name="Bank">Banka tablosunun satırları (geri okunmuş).</param>
/// <param name="FreeImages">Serbest görseller (geri okunmuş).</param>
public sealed record OverridesInspection(bool Present, int BlockCount, int DanglingMarkers, int Version, bool Keepable,
    string? Reason, IReadOnlyList<OverridesImageInfo> Images, int BankRows,
    bool Recognized = false, IReadOnlyList<ThemeAssetKind>? Hide = null, IReadOnlyList<BankInfoInput>? Bank = null,
    IReadOnlyList<OverridesFreeImageInfo>? FreeImages = null);

public sealed record OverridesApplyResult(bool Success, string Xslt, IReadOnlyList<string> OccupiedHolderIds,
    int ImagesKept, int BankRowsKept, string? Error);

/// <summary>
/// Yüklenen temadaki önceden gömülü görsel bloğu (<c>DEV-MODE-OVERRIDES</c>) için: incele, kaldır ya da koru.
/// Koru: bloktaki her görsel hedef tutucusunun içine <b>statik</b> <c>&lt;img&gt;</c> olarak yazılır, tutucu
/// <c>data-xc-occupied</c> ile işaretlenir (konum olarak sunulmaz, runtime ona yazmaz) ve betik silinir;
/// banka tablosu temanın kalıcı parçası olur. Böylece kullanıcı kendi logosunu eklediğinde tasarım görseli kaybolmaz.
/// </summary>
public interface IThemeOverridesProcessor
{
    OverridesInspection Inspect(string xslt);
    OverridesApplyResult Apply(string xslt, OverridesDecision decision);
}

public sealed class ThemeOverridesProcessor(IThemeImageValidator images) : IThemeOverridesProcessor
{
    public OverridesInspection Inspect(string xslt) => InspectCore(xslt).Inspection;

    public OverridesApplyResult Apply(string xslt, OverridesDecision decision)
    {
        if (decision == OverridesDecision.Strip)
            return new OverridesApplyResult(true, OverridesBlockText.RemoveAll(xslt), [], 0, 0, null);

        var (inspection, parsed, holders, map) = InspectCore(xslt);
        if (!inspection.Present)
            return new OverridesApplyResult(true, OverridesBlockText.RemoveAll(xslt), [], 0, 0, null);
        if (!inspection.Keepable || parsed is null || map is null)
            return new OverridesApplyResult(false, xslt, [], 0, 0, inspection.Reason ?? "Blok korunamıyor.");

        var block = OverridesBlockText.Scan(xslt).Blocks[0];
        var edits = new SourceEdits();
        var occupied = new List<string>();

        // Aynı tutucuya düşen görseller tek içerikte alt alta.
        foreach (var group in holders.GroupBy(h => h.Holder, ReferenceEqualityComparer.Instance))
        {
            var holder = (XElement)group.Key!;
            var span = map.Span(holder);
            var content = string.Concat(group.Select(g => StaticImage(g.Image)));
            var id = (string)holder.Attribute("id")!;
            occupied.Add(id);

            if (span.SelfClosing)
            {
                var tag = map.Source[span.Start..span.End].TrimEnd();
                tag = tag[..^2].TrimEnd(); // "/>" kaldır
                edits.Replace(span.Start, span.End,
                    $@"{tag} {ThemeSlotMarkers.OccupiedAttribute}=""1"">{content}</{holder.Name.LocalName}>");
            }
            else
            {
                edits.Insert(NameEnd(map.Source, span.Start), $@" {ThemeSlotMarkers.OccupiedAttribute}=""1""");
                edits.Replace(span.StartTagEnd, span.EndTagStart, content);
            }
        }

        // Blok → (varsa) kalıcı banka tablosu; eşleşmeyen işaretler de silinir.
        edits.Replace(block.Start, block.End, parsed.BankTableXml ?? string.Empty);
        foreach (var (start, length) in OverridesBlockText.Scan(xslt).Dangling)
            edits.Replace(start, start + length, string.Empty);

        string result;
        try
        {
            result = edits.Apply(xslt);
        }
        catch (InvalidOperationException ex)
        {
            return new OverridesApplyResult(false, xslt, [], 0, 0, ex.Message);
        }

        if (XsltSafety.FindThreat(result) is { } threat)
            return new OverridesApplyResult(false, xslt, [], 0, 0, threat);

        return new OverridesApplyResult(true, result, occupied.Distinct().ToList(), parsed.Images.Count, parsed.BankRows, null);
    }

    private (OverridesInspection Inspection, ParsedOverrides? Parsed, List<(XElement Holder, ParsedImage Image)> Holders, XsltSourceMap? Map)
        InspectCore(string xslt)
    {
        var scan = OverridesBlockText.Scan(xslt);
        var none = new List<(XElement, ParsedImage)>();
        if (scan.Blocks.Count == 0)
            return (new OverridesInspection(false, 0, scan.Dangling.Count, 0, false, null, [], 0), null, none, null);

        if (scan.Blocks.Count > 1)
            return (NotKeepable(scan, 0, "Temada birden fazla görsel bloğu var; yalnız kaldırılabilir."), null, none, null);

        var b = scan.Blocks[0];
        var parsed = OverridesBlockParser.Parse(xslt[b.InnerStart..b.InnerEnd]);
        if (!parsed.Recognized)
            return (NotKeepable(scan, parsed.Version, parsed.Reason!), parsed, none, null);

        var bank = BankTableReader.Read(parsed.BankTableXml);
        var free = parsed.FreeImages.Select(FreeInfo).ToList();
        if (!XsltSourceMap.TryBuild(xslt, out var map, out var error))
        {
            var unparsed = NotKeepable(scan, parsed.Version, $"Tema ayrıştırılamadı: {error}");
            return (unparsed with { Recognized = true, Hide = parsed.Hide, Bank = bank, FreeImages = free }, parsed, none, null);
        }

        var body = map.Document.Descendants().FirstOrDefault(e => ThemeDocument.IsLiteral(e, "body"));
        var infos = new List<OverridesImageInfo>();
        var holders = new List<(XElement, ParsedImage)>();
        string? reason = null;

        foreach (var image in parsed.Images)
        {
            var check = images.Check(image.DataUrl);
            var targets = check.Valid ? ResolveTargets(body, image) : [];
            string? problem = !check.Valid ? check.Error
                : targets.Count == 0 ? "Görselin hedef alanı temada boş bir tutucu olarak bulunamadı."
                : null;

            reason ??= problem;
            holders.AddRange(targets.Select(t => (t, image)));
            infos.Add(new OverridesImageInfo(image.Kind, image.Targets, check.Valid ? image.DataUrl : null,
                image.Width, image.Height, image.Alignment.ToString().ToLowerInvariant(), problem is null, problem,
                image.Slot, image.ExtraSlots, image.Keep));
        }

        // Ek yuva / keep statik görsele çevrilemez (koru yalnız tek hedef yazar).
        if (parsed.Images.Any(i => i.ExtraSlots.Count > 0 || i.Keep))
            reason ??= "Blokta ek konuma kopyalanan ya da temadaki görseli koruyan bir görsel var; yalnız kaldırılabilir.";
        // Serbest görsel temanın parçası yapılmaz.
        if (free.Count > 0)
            reason ??= "Blokta serbest görsel var; yalnız kaldırılabilir.";

        var inspection = new OverridesInspection(true, 1, scan.Dangling.Count, parsed.Version, reason is null,
            reason, infos, parsed.BankRows, Recognized: true, parsed.Hide, bank, free);
        return (inspection, parsed, holders, map);
    }

    private OverridesFreeImageInfo FreeInfo(ParsedFreeImage image)
    {
        var check = images.Check(image.DataUrl);
        return new OverridesFreeImageInfo(image.Slot, check.Valid ? image.DataUrl : null, image.Width, image.Height,
            image.Alignment.ToString().ToLowerInvariant(), check.Valid, check.Valid ? null : check.Error);
    }

    // v1: listedeki mevcut her id (betik ids.forEach ile hepsine yazıyordu). v2: önce yuva, yoksa ilk yedek id.
    // Hedef gövdede tam bir kez geçmeli ve içeriği boş olmalı; void eleman (ör. temadaki <img>) içerik alamaz.
    private static List<XElement> ResolveTargets(XElement? body, ParsedImage image)
    {
        if (body is null)
            return [];

        List<XElement> Usable(Func<XElement, bool> match)
        {
            var found = body.DescendantsAndSelf().Where(e => ThemeDocument.IsLiteral(e) && match(e)).ToList();
            return found.Count == 1 && !ThemeDocument.IsVoidElement(found[0]) && ThemeDocument.HasOnlyBlankContent(found[0])
                   && found[0].Attribute(ThemeSlotMarkers.OccupiedAttribute) is null
                ? found
                : [];
        }

        if (image.Slot is not null)
        {
            var bySlot = Usable(e => (string?)e.Attribute(ThemeSlotMarkers.SlotAttribute) == image.Slot);
            if (bySlot.Count == 1)
                return bySlot;
        }

        var result = new List<XElement>();
        foreach (var id in image.Targets)
        {
            var byId = Usable(e => (string?)e.Attribute("id") == id);
            result.AddRange(byId);
            if (image.Slot is not null && byId.Count > 0)
                break; // v2 runtime ilk bulunan yedeğe yazar
        }
        return result;
    }

    private static string StaticImage(ParsedImage image)
    {
        var jc = image.Alignment switch
        {
            ThemeAlignment.Center => "center",
            ThemeAlignment.Right => "flex-end",
            _ => "flex-start",
        };
        var w = image.Width is { } width ? $"width:{width}px;" : "width:auto;";
        var h = image.Height is { } height ? $"height:{height}px;" : "height:auto;";
        return $@"<div style=""display:flex;justify-content:{jc};align-items:center;""><img src=""{image.DataUrl}"" alt="""" style=""{w}{h}max-width:100%;object-fit:contain;display:block;""/></div>";
    }

    private static OverridesInspection NotKeepable(BlockScan scan, int version, string reason) =>
        new(true, scan.Blocks.Count, scan.Dangling.Count, version, false, reason, [], 0);

    private static int NameEnd(string source, int start)
    {
        var i = start + 1;
        while (i < source.Length && !char.IsWhiteSpace(source[i]) && source[i] is not ('>' or '/'))
            i++;
        return i;
    }
}
