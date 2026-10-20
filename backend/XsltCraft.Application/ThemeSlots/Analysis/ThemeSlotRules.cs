using System.Xml.Linq;

using XsltCraft.Domain.Entities;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>Bir kuralın önerdiği yuva. Ya yeni işaret eklenecek bir nokta (<see cref="Point"/>), ya nitelik
/// eklenerek benimsenecek mevcut bir tutucu (<see cref="AdoptedHolder"/>) ya da yerini kullanıcının görselinin
/// alacağı temadaki statik görseller (<see cref="Placeholders"/>, aynı yuva kimliğini paylaşır) taşır.</summary>
internal sealed record SlotCandidate(
    string Id, string Rule, int Order, SlotLayout Layout,
    InsertionPoint? Point, XElement? AdoptedHolder,
    ThemeAssetKind? DefaultKind, int DefaultPriority,
    IReadOnlyList<XElement>? Placeholders = null, ThemeAssetKind? PlaceholderKind = null,
    IReadOnlyList<XElement>? RemovablePlaceholders = null)
{
    public bool Conditional => Point?.Conditional
        ?? Placeholders?.All(InsertionPointResolver.IsConditional)
        ?? false;
}

/// <summary>
/// Yuva kuralları R1–R9: zorunlu alanlara (çapalara) göre logo/imza konulabilecek konumlar.
/// Kural bir çapanın yanında zaten boş bir eski tutucu (<c>#company_logo</c>, <c>#imza</c> …) bulursa yeni işaret
/// eklemez, o tutucuyu benimser — böylece varsayılan görünüm bugünküyle aynı kalır.
/// </summary>
internal sealed class ThemeSlotRules(ThemeDocument d, ThemeAnchors a, InsertionPointResolver resolver,
    IReadOnlyList<(DetectedImage Image, ThemeImageRole Role, bool Removable)>? images = null)
{
    private readonly HashSet<XElement> _holders = new(a.Holders, ReferenceEqualityComparer.Instance);

    public List<SlotCandidate> Propose(List<ThemeSlotWarning> warnings)
    {
        var result = new List<SlotCandidate>();
        result.AddRange(ExistingImages());

        void Add(string id, string rule, SlotCandidate? candidate)
        {
            if (candidate is not null)
                result.Add(candidate);
            else
                warnings.Add(new ThemeSlotWarning("rule-skipped", $"{rule} ({id}) için uygun bir ekleme noktası bulunamadı.", id));
        }

        if (a.Seller is not null)
            Add("seller-top", "R1", SellerTop(a.Seller));
        if (a.GibLogo is not null)
            Add("gib-logo-below", "R2", GibLogoBelow(a.GibLogo));
        if (a.Qr is not null)
            Add("qr-below", "R3", QrBelow(a.Qr));
        if (a.Customer is not null && CustomerSide(a.Customer) is { } side)
            result.Add(side); // boş komşu hücre yoksa kural sessizce uygulanmaz (tasarım gereği)
        if (a.DocInfo is not null)
            Add("docinfo-top", "R5", DocInfoTop(a.DocInfo));
        if (a.Totals is not null && d.DocumentType == DocumentType.Invoice)
            Add("totals-left", "R6", TotalsLeft(a.Totals));
        if (a.Notes is not null)
        {
            Add("notes-bottom", "R7", Point("notes-bottom", "R7", 70, a.Notes, PointKind.After, SlotLayout.Block, ThemeAssetKind.Signature, 3));
            if (NotesSide(a.Notes) is { } notesSide)
                result.Add(notesSide);
        }

        result.AddRange(LegacyHolders(result));
        return result;
    }

    // R1 — satıcı hücresinin başı. Hücrenin ilk literal çocuğu boş tutucuysa onu benimser.
    private SlotCandidate? SellerTop(XElement seller)
    {
        if (LiteralChildren(seller).FirstOrDefault() is { } first && _holders.Contains(first))
            return Adopt("seller-top", "R1", 10, first, ThemeAssetKind.Logo, 2);
        return Point("seller-top", "R1", 10, seller, PointKind.ContentStart, SlotLayout.Block, ThemeAssetKind.Logo, 2);
    }

    // R2 — GİB logosu + belge başlığı hücresinin sonu.
    private SlotCandidate? GibLogoBelow(XElement cell)
    {
        if (LiteralChildren(cell).LastOrDefault() is { } last && _holders.Contains(last))
            return Adopt("gib-logo-below", "R2", 20, last, null, 0);
        return Point("gib-logo-below", "R2", 20, cell, PointKind.ContentEnd, SlotLayout.Block, null, 0);
    }

    // R3 — karekodun altı. QR'ın tablosunda (yoksa ebeveyninde) QR'dan sonra gelen ilk boş tutucu benimsenir.
    private SlotCandidate? QrBelow(XElement qr)
    {
        var scope = LiteralAncestor(qr, "table") ?? qr.Parent;
        var after = scope?.Descendants().SkipWhile(e => !ReferenceEquals(e, qr)).Skip(1)
            .FirstOrDefault(e => _holders.Contains(e) && !e.Ancestors().Contains(qr));
        if (after is not null)
            return Adopt("qr-below", "R3", 30, after, null, 0);

        var style = ((string?)qr.Attribute("style") ?? "").ToLowerInvariant();
        var layout = style.Contains("float") || qr.Attribute("align") is not null ? SlotLayout.BlockClear : SlotLayout.Block;
        return Point("qr-below", "R3", 30, qr, PointKind.After, layout, null, 0);
    }

    // R4 — alıcının yanındaki boş hücre (aynı satırda sonraki, yoksa önceki hücre).
    private SlotCandidate? CustomerSide(XElement customer)
    {
        var cell = LiteralAncestorOrSelf(customer, "td") ?? LiteralAncestorOrSelf(customer, "th");
        var row = cell is null ? null : LiteralAncestor(cell, "tr");
        if (cell is null || row is null)
            return null;

        var siblings = LiteralChildren(row).Where(c => IsLiteral(c, "td") || IsLiteral(c, "th")).ToList();
        var idx = siblings.IndexOf(cell);
        var neighbour = siblings.ElementAtOrDefault(idx + 1) is { } next && IsBlankCell(next)
            ? next
            : idx > 0 && IsBlankCell(siblings[idx - 1]) ? siblings[idx - 1] : null;
        if (neighbour is null)
            return null;

        var holder = neighbour.Descendants().FirstOrDefault(_holders.Contains);
        return holder is not null
            ? Adopt("customer-side", "R4", 40, holder, ThemeAssetKind.Signature, 1)
            : Point("customer-side", "R4", 40, neighbour, PointKind.ContentStart, SlotLayout.Block, ThemeAssetKind.Signature, 1);
    }

    // R5 — belge bilgileri tablosunun üstü. Hemen önceki kardeş boş tutucuysa onu benimser.
    private SlotCandidate? DocInfoTop(XElement table)
    {
        var previous = table.ElementsBeforeSelf().LastOrDefault();
        if (previous is not null && _holders.Contains(previous) && OnlyBlankBetween(previous, table))
            return Adopt("docinfo-top", "R5", 50, previous, ThemeAssetKind.Logo, 1);
        return Point("docinfo-top", "R5", 50, table, PointKind.Before, SlotLayout.Block, ThemeAssetKind.Logo, 1);
    }

    // R6 — toplamların solu: ödenecek tutar tablosunun hücresinin başına, sola yaslı.
    private SlotCandidate? TotalsLeft(XElement container)
    {
        var payable = container.DescendantsAndSelf()
            .Where(e => IsLiteral(e, "table") && e.Descendants().Any(x => IsXsl(x)
                && Collapse((string?)x.Attribute("select") ?? "").Contains("PayableAmount", StringComparison.Ordinal)))
            .LastOrDefault();
        if (payable is null || ReferenceEquals(payable, container))
            return Point("totals-left", "R6", 60, container, PointKind.Before, SlotLayout.FloatLeft, ThemeAssetKind.Signature, 2);

        var cell = payable.Ancestors().TakeWhile(x => !ReferenceEquals(x, container))
            .FirstOrDefault(x => IsLiteral(x, "td") || IsLiteral(x, "th"));
        return cell is null
            ? Point("totals-left", "R6", 60, payable, PointKind.Before, SlotLayout.FloatLeft, ThemeAssetKind.Signature, 2)
            : Point("totals-left", "R6", 60, cell, PointKind.ContentStart, SlotLayout.FloatLeft, ThemeAssetKind.Signature, 2);
    }

    // R8 — not satırındaki boş komşu hücre (QR içermeyen).
    private SlotCandidate? NotesSide(XElement notesTable)
    {
        var rows = notesTable.Descendants().Where(e => IsLiteral(e, "tr"))
            .Where(r => ReferenceEquals(LiteralAncestor(r, "table"), notesTable));
        foreach (var row in rows)
        {
            var cells = LiteralChildren(row).Where(c => IsLiteral(c, "td") || IsLiteral(c, "th")).ToList();
            if (cells.Count < 2)
                continue;
            var blank = cells.FirstOrDefault(c => IsBlankCell(c) && !c.Descendants().Any(x => (string?)x.Attribute("id") == "qrcode"));
            if (blank is null)
                continue;
            var holder = blank.Descendants().FirstOrDefault(_holders.Contains);
            return holder is not null
                ? Adopt("notes-side", "R8", 80, holder, null, 0)
                : Point("notes-side", "R8", 80, blank, PointKind.ContentStart, SlotLayout.Block, null, 0);
        }
        return null;
    }

    // R9 — hiçbir kuralın benimsemediği boş eski tutucular kendi başına yuva olur.
    private IEnumerable<SlotCandidate> LegacyHolders(List<SlotCandidate> existing)
    {
        var adopted = existing.Select(c => c.AdoptedHolder).OfType<XElement>().ToHashSet(ReferenceEqualityComparer.Instance);
        var headerTable = d.Body.Descendants().FirstOrDefault(e => IsLiteral(e, "table")
            && e.Descendants().Any(x => ReferenceEquals(x, a.Seller) || ReferenceEquals(x, a.GibLogo) || ReferenceEquals(x, a.Qr)));

        var order = 90;
        foreach (var holder in a.Holders.Where(h => !adopted.Contains(h)))
        {
            var kind = LegacyHolderIds.KindOf((string)holder.Attribute("id")!)!.Value;
            var id = kind == ThemeAssetKind.Logo ? "legacy-logo" : "legacy-signature";

            if (headerTable is not null && holder.Ancestors().Contains(headerTable))
            {
                var cell = LiteralAncestor(holder, "td");
                var row = cell is null ? null : LiteralAncestor(cell, "tr");
                if (cell is not null && row is not null)
                {
                    var cells = LiteralChildren(row).Where(c => IsLiteral(c, "td")).ToList();
                    var i = cells.IndexOf(cell);
                    id = i == 0 ? "header-left" : i == cells.Count - 1 ? "header-right" : "header-center";
                }
            }

            if (Adopt(id, "R9", order++, holder, kind, 4) is { } candidate)
                yield return candidate;
        }
    }

    // R10 — admin'in logo/imza olarak işaretlediği statik görseller: aynı roldekiler tek yuva, varsayılan o rol.
    private IEnumerable<SlotCandidate> ExistingImages()
    {
        foreach (var (kind, role, id, order) in new[]
        {
            (ThemeAssetKind.Logo, ThemeImageRole.Logo, "existing-logo", 1),
            (ThemeAssetKind.Signature, ThemeImageRole.Signature, "existing-signature", 2),
        })
        {
            var matching = (images ?? []).Where(i => i.Role == role).ToList();
            if (matching.Count > 0)
                yield return new SlotCandidate(id, "R10", order, SlotLayout.Block, null, null, kind, 0,
                    matching.Select(i => i.Image.Element).ToList(), kind,
                    matching.Where(i => i.Removable).Select(i => i.Image.Element).ToList());
        }
    }

    // ── yardımcılar ───────────────────────────────────────────────────────────

    private SlotCandidate? Point(string id, string rule, int order, XElement target, PointKind kind,
        SlotLayout layout, ThemeAssetKind? defaultKind, int priority)
    {
        var point = resolver.Resolve(target, kind);
        return point is null ? null : new SlotCandidate(id, rule, order, layout, point, null, defaultKind, priority);
    }

    private SlotCandidate? Adopt(string id, string rule, int order, XElement holder, ThemeAssetKind? defaultKind, int priority)
    {
        // Tutucunun kendisi geçerli bir bağlamda olmalı (ör. tekrarlayan döngüde değil).
        if (holder.Parent is null || !resolver.IsValid(holder.Parent))
            return null;
        var kind = defaultKind ?? LegacyHolderIds.KindOf((string?)holder.Attribute("id") ?? "");
        return new SlotCandidate(id, rule, order, SlotLayout.Block, null, holder, kind, priority);
    }

    // Hücre boş sayılır: yalnız boşluk/yorum ve boş eski tutucular.
    private bool IsBlankCell(XElement cell) => cell.Nodes().All(n => n switch
    {
        XText t => string.IsNullOrWhiteSpace(t.Value),
        XComment => true,
        XElement e => _holders.Contains(e) || (IsLiteral(e, "div") && HasOnlyBlankContent(e) && e.Attribute("id") is null),
        _ => false,
    });

    private static bool OnlyBlankBetween(XElement first, XElement second)
    {
        for (var n = first.NextNode; n is not null && !ReferenceEquals(n, second); n = n.NextNode)
        {
            if (n is XText t && string.IsNullOrWhiteSpace(t.Value)) continue;
            if (n is XComment) continue;
            return false;
        }
        return true;
    }
}
