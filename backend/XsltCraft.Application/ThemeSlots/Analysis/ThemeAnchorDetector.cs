using System.Text.RegularExpressions;
using System.Xml.Linq;

using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>Zorunlu alanların (çapaların) temadaki yeri. Bulunamayan çapa null.</summary>
internal sealed class ThemeAnchors
{
    public XElement? Seller { get; init; }
    public XElement? GibLogo { get; init; }

    /// <summary>GİB logosunun kendisi (statik görsel tespitinde hariç tutulur).</summary>
    public XElement? GibLogoImage { get; init; }
    public XElement? Qr { get; init; }
    public XElement? Customer { get; init; }
    public XElement? DocInfo { get; init; }
    public XElement? Ettn { get; init; }
    public XElement? Lines { get; init; }
    public XElement? Totals { get; init; }
    public XElement? Notes { get; init; }

    /// <summary>Boş, kullanılabilir eski tutucular (<c>#company_logo</c>, <c>#imza</c> …).</summary>
    public IReadOnlyList<XElement> Holders { get; init; } = [];

    public required IReadOnlyList<ThemeAnchorResult> Report { get; init; }
}

/// <summary>
/// Zorunlu alanları XSLT kaynağı üzerinden bulur. Öncelik: kararlı id'ler → XPath imzaları → yorumlar
/// (yalnız eşitlik bozucu; tema yorumları güvenilir değil).
/// </summary>
internal sealed class ThemeAnchorDetector
{
    [Flags]
    private enum Ref
    {
        None = 0,
        Supplier = 1 << 0,
        Customer = 1 << 1,
        Lines = 1 << 2,
        Totals = 1 << 3,
        Payable = 1 << 4,
        Customization = 1 << 5,
        Profile = 1 << 6,
        IssueDate = 1 << 7,
    }

    private static readonly Regex QrScript = new(
        @"new\s+QRCode\s*\(\s*document\.getElementById\(\s*[""']([A-Za-z_][\w-]*)[""']\s*\)", RegexOptions.CultureInvariant);

    private static readonly string[] DocumentHeadings =
        ["e-FATURA", "e-Fatura", "e-ARŞİV", "e-Arşiv", "e-ARSIV", "e-Arsiv", "e-İRSALİYE", "e-İrsaliye", "e-IRSALIYE",
         "e-Irsaliye", "e-GİDER", "e-Gider", "e-GIDER", "e-SMM", "e-MÜSTAHSİL"];

    private readonly Dictionary<XElement, Ref> _refs = new(ReferenceEqualityComparer.Instance);

    public ThemeAnchors Detect(ThemeDocument d)
    {
        var bodyElements = d.Body.DescendantsAndSelf().Where(e => !IsExcluded(e)).ToList();
        var literal = bodyElements.Where(e => IsLiteral(e)).ToList();
        var despatch = d.DocumentType == DocumentType.Despatch;

        var report = new List<ThemeAnchorResult>();
        ThemeAnchorResult Found(string id, XElement? e, string? via) =>
            new(id, e is not null, e is null ? null : d.Map.LineOf(d.Map.Span(e).Start), e is null ? null : via);

        // ── QR ────────────────────────────────────────────────────────────────
        var qr = literal.FirstOrDefault(e => (string?)e.Attribute("id") == "qrcode");
        var qrVia = "id:qrcode";
        if (qr is null)
        {
            var scriptId = d.RootTemplate.Descendants().Where(e => IsLiteral(e, "script"))
                .Select(s => QrScript.Match(s.Value)).FirstOrDefault(m => m.Success)?.Groups[1].Value;
            if (scriptId is not null)
            {
                qr = literal.FirstOrDefault(e => (string?)e.Attribute("id") == scriptId);
                qrVia = $"script:{scriptId}";
            }
        }
        report.Add(Found("qr", qr, qrVia));

        // ── Satıcı / gönderici ────────────────────────────────────────────────
        var cells = literal.Where(e => IsLiteral(e, "td") || IsLiteral(e, "th")).ToList();
        var sellerCandidates = cells
            .Where(c => Has(c, Ref.Supplier) && !Has(c, Ref.Customer) && !Has(c, Ref.Lines) && !Has(c, Ref.Totals))
            .ToList();
        var seller = sellerCandidates.FirstOrDefault(c =>
            !c.Ancestors().Any(a => sellerCandidates.Contains(a)));
        report.Add(Found("seller", seller, despatch ? "xpath:DespatchSupplierParty" : "xpath:AccountingSupplierParty"));

        // ── GİB logosu + belge başlığı ────────────────────────────────────────
        // Başlığı en yakın hücresinde taşıyan görsel önce: aynı büyük hücredeki firma logosu GİB logosu sanılmasın.
        XElement? gibCell = null, gibImage = null;
        var dataImages = literal.Where(e => IsLiteral(e, "img")
            && ((string?)e.Attribute("src") ?? "").StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)).ToList();
        for (var depth = 0; depth < 3 && gibCell is null; depth++)
        {
            foreach (var img in dataImages)
            {
                var cell = img.Ancestors().Where(a => IsLiteral(a, "td") || IsLiteral(a, "th")).ElementAtOrDefault(depth);
                if (cell is not null && DocumentHeadings.Any(h => cell.Value.Contains(h, StringComparison.Ordinal)))
                {
                    (gibCell, gibImage) = (cell, img);
                    break;
                }
            }
        }
        report.Add(Found("gibLogo", gibCell, "img+heading"));

        // ── Alıcı ─────────────────────────────────────────────────────────────
        XElement? customer = null;
        var customerTable = literal.FirstOrDefault(e => (string?)e.Attribute("id") == "customerPartyTable");
        var customerVia = "id:customerPartyTable";
        if (customerTable is not null)
        {
            customer = LiteralAncestorOrSelf(customerTable, "td") ?? customerTable;
        }
        else
        {
            var candidates = cells
                .Where(c => Has(c, Ref.Customer) && !Has(c, Ref.Supplier) && !Has(c, Ref.Lines))
                .ToList();
            customer = candidates.FirstOrDefault(c => !c.Ancestors().Any(a => candidates.Contains(a)));
            customerVia = despatch ? "xpath:DeliveryCustomerParty" : "xpath:AccountingCustomerParty";
        }
        report.Add(Found("customer", customer, customerVia));

        // ── Belge bilgileri (Özelleştirme No, Senaryo, Tarih …) ────────────────
        var docInfoCandidates = literal
            .Where(e => IsLiteral(e, "table") && Has(e, Ref.Customization | Ref.Profile | Ref.IssueDate, all: true))
            .ToList();
        var docInfo = docInfoCandidates.FirstOrDefault(t => !docInfoCandidates.Any(o => !ReferenceEquals(o, t) && o.Ancestors().Contains(t)));
        report.Add(Found("docInfo", docInfo, "xpath:CustomizationID+ProfileID+IssueDate"));

        // ── ETTN (yalnız rapor) ───────────────────────────────────────────────
        var uuid = bodyElements.FirstOrDefault(e => (IsXsl(e, "value-of") || IsXsl(e, "for-each"))
            && Collapse((string?)e.Attribute("select") ?? "").Contains("cbc:UUID", StringComparison.Ordinal));
        var ettn = uuid is null ? null : LiteralAncestor(uuid, "table") ?? LiteralParent(uuid);
        report.Add(Found("ettn", ettn, "xpath:cbc:UUID"));

        // ── Kalemler (yalnız rapor; id'ye güvenilmez) ──────────────────────────
        var linesLoop = bodyElements.FirstOrDefault(e =>
            (IsXsl(e, "for-each") || IsXsl(e, "apply-templates"))
            && Collapse((string?)e.Attribute("select") ?? "") is var sel
            && (sel.Contains("InvoiceLine", StringComparison.Ordinal) || sel.Contains("DespatchLine", StringComparison.Ordinal)));
        var lines = linesLoop is null ? null : LiteralAncestor(linesLoop, "table") ?? LiteralParent(linesLoop);
        report.Add(Found("lines", lines, despatch ? "xpath:DespatchLine" : "xpath:InvoiceLine"));

        // ── Toplamlar (yalnız fatura) ─────────────────────────────────────────
        XElement? totals = null;
        if (!despatch)
        {
            totals = literal.FirstOrDefault(e => (string?)e.Attribute("id") == "budgetContainerTable");
            var totalsVia = "id:budgetContainerTable";
            if (totals is null)
            {
                var payable = literal.Where(e => IsLiteral(e, "table") && Has(e, Ref.Payable)).ToList();
                totals = payable.FirstOrDefault(t => !payable.Any(o => !ReferenceEquals(o, t) && o.Ancestors().Contains(t)));
                totalsVia = "xpath:PayableAmount";
            }
            report.Add(Found("totals", totals, totalsVia));
        }

        // ── Notlar ────────────────────────────────────────────────────────────
        var notes = literal.FirstOrDefault(e => (string?)e.Attribute("id") == "notesTable");
        var notesVia = "id:notesTable";
        if (notes is null)
        {
            var loop = XsltNoteLoops.FindUnconditionalNoteLoop(d.Map.Document);
            notes = loop is not null && d.IsInBody(loop) ? LiteralAncestor(loop, "table") : null;
            notesVia = "xpath:cbc:Note";
        }
        report.Add(Found("notes", notes, notesVia));

        // ── Eski tutucular ────────────────────────────────────────────────────
        // Void eleman (ör. <img id="company_logo">) boş görünür ama içerik alamaz: tutucu değil, temadaki görseldir.
        var holders = literal
            .Where(e => (string?)e.Attribute("id") is { } id && LegacyHolderIds.KindOf(id) is not null)
            .Where(e => !IsVoidElement(e) && e.Attribute("data-xc-occupied") is null && HasOnlyBlankContent(e))
            .ToList();

        return new ThemeAnchors
        {
            Seller = seller,
            GibLogo = gibCell,
            GibLogoImage = gibImage,
            Qr = qr,
            Customer = customer,
            DocInfo = docInfo,
            Ettn = ettn,
            Lines = lines,
            Totals = totals,
            Notes = notes,
            Holders = holders,
            Report = report,
        };
    }

    /// <summary>İçeriği dolu olduğu için yuva sayılmayan eski tutucular (rapor uyarısı için).</summary>
    public static IEnumerable<XElement> FilledHolders(ThemeDocument d) =>
        d.Body.Descendants()
            .Where(e => IsLiteral(e) && (string?)e.Attribute("id") is { } id && LegacyHolderIds.KindOf(id) is not null)
            .Where(e => !IsVoidElement(e) && e.Attribute("data-xc-occupied") is null && !HasOnlyBlankContent(e));

    private bool Has(XElement e, Ref flags, bool all = false)
    {
        var r = Refs(e);
        return all ? (r & flags) == flags : (r & flags) != 0;
    }

    // Elemanın kendisi ve torunlarındaki XSL select/test/match ifadelerinden çıkan alan imzaları (memo).
    private Ref Refs(XElement e)
    {
        if (_refs.TryGetValue(e, out var cached))
            return cached;

        var r = Ref.None;
        if (IsXsl(e))
        {
            foreach (var name in new[] { "select", "test", "match" })
            {
                if (e.Attribute(name) is { } a)
                    r |= Classify(Collapse(a.Value));
            }
        }
        foreach (var child in e.Elements())
            r |= Refs(child);

        _refs[e] = r;
        return r;
    }

    private static Ref Classify(string expr)
    {
        var r = Ref.None;
        if (expr.Contains("AccountingSupplierParty", StringComparison.Ordinal) || expr.Contains("DespatchSupplierParty", StringComparison.Ordinal))
            r |= Ref.Supplier;
        if (expr.Contains("AccountingCustomerParty", StringComparison.Ordinal) || expr.Contains("DeliveryCustomerParty", StringComparison.Ordinal)
            || expr.Contains("BuyerCustomerParty", StringComparison.Ordinal))
            r |= Ref.Customer;
        if (expr.Contains("InvoiceLine", StringComparison.Ordinal) || expr.Contains("DespatchLine", StringComparison.Ordinal))
            r |= Ref.Lines;
        if (expr.Contains("LegalMonetaryTotal", StringComparison.Ordinal))
            r |= Ref.Totals;
        if (expr.Contains("PayableAmount", StringComparison.Ordinal))
            r |= Ref.Payable;
        if (expr.Contains("CustomizationID", StringComparison.Ordinal))
            r |= Ref.Customization;
        if (expr.Contains("ProfileID", StringComparison.Ordinal))
            r |= Ref.Profile;
        if (expr.Contains("IssueDate", StringComparison.Ordinal))
            r |= Ref.IssueDate;
        return r;
    }
}
