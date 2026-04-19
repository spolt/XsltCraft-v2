using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace XsltCraft.Application.Validation;

/// <summary>
/// UBL-TR 1.2 Fatura için ~15 iş kuralı çalıştırır (VKN/TCKN checksum, ProfileID,
/// InvoiceTypeCode, KDV tutarlılığı, zorunlu alan kontrolleri).
/// Saf C# — harici bağımlılık yok.
/// </summary>
public class UblTrBusinessRuleService : IUblTrBusinessRuleService
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    private static readonly HashSet<string> ValidProfileIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "TEMELFATURA", "TICARIFATURA", "EARSIVFATURA", "IHRACAT",
        "YOLCUBERABERFATURA", "KAMU", "HKS", "KONAKLAMAVERGISI"
    };

    private static readonly HashSet<string> ValidInvoiceTypeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SATIS", "IADE", "TEVKIFAT", "ISTISNA", "OZELMATRAH",
        "IHRACKAYITLI", "SGK", "KOMISYONCU", "HKS"
    };

    private static readonly Regex InvoiceIdRegex = new(@"^[A-Z]{3}\d{13}$", RegexOptions.Compiled);
    private static readonly Regex IssueDateRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public IReadOnlyList<BusinessRuleResult> Validate(string xmlContent)
    {
        var results = new List<BusinessRuleResult>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            results.Add(new BusinessRuleResult(
                "UBL-XML", "XML Ayrıştırma", BusinessRuleSeverity.Error,
                $"XML ayrıştırılamadı: {ex.Message}", ex.LineNumber, ex.LinePosition));
            return results;
        }

        var root = doc.Root;
        if (root is null)
        {
            results.Add(new BusinessRuleResult(
                "UBL-XML", "XML Ayrıştırma", BusinessRuleSeverity.Error, "XML içeriği boş."));
            return results;
        }

        CheckUblVersion(root, results);
        CheckCustomizationId(root, results);
        CheckProfileId(root, results, out var profileId);
        CheckInvoiceTypeCode(root, results, out var invoiceTypeCode);
        CheckProfileInvoiceTypeCombo(profileId, invoiceTypeCode, root, results);
        CheckInvoiceId(root, results);
        CheckUuid(root, results);
        CheckIssueDate(root, results);
        CheckCurrencyCode(root, results);
        CheckPartyIdentification(root, "AccountingSupplierParty", "UBL-010", "Satıcı", results, requireTaxId: true);
        CheckPartyIdentification(root, "AccountingCustomerParty", "UBL-011", "Alıcı", results, requireTaxId: false);
        CheckTaxIdChecksums(root, results);
        CheckInvoiceLinesExist(root, results, out var lineCount);
        CheckMonetaryTotals(root, results);

        return results;
    }

    // ── UBL-001: UBLVersionID = 2.1 ────────────────────────────────────────
    private static void CheckUblVersion(XElement root, List<BusinessRuleResult> results)
    {
        var version = root.Element(Cbc + "UBLVersionID")?.Value?.Trim();
        if (string.IsNullOrEmpty(version))
        {
            results.Add(Make("UBL-001", "UBL Versiyonu", BusinessRuleSeverity.Error,
                "UBLVersionID elementi eksik (2.1 olmalı).", root.Element(Cbc + "UBLVersionID") ?? root));
        }
        else if (version != "2.1")
        {
            results.Add(Make("UBL-001", "UBL Versiyonu", BusinessRuleSeverity.Warning,
                $"UBLVersionID '{version}' — UBL-TR için beklenen '2.1'.", root.Element(Cbc + "UBLVersionID")!));
        }
    }

    // ── UBL-002: CustomizationID TR ile başlamalı ──────────────────────────
    private static void CheckCustomizationId(XElement root, List<BusinessRuleResult> results)
    {
        var el = root.Element(Cbc + "CustomizationID");
        var val = el?.Value?.Trim();
        if (string.IsNullOrEmpty(val))
        {
            results.Add(Make("UBL-002", "CustomizationID", BusinessRuleSeverity.Error,
                "CustomizationID eksik (örn. TR1.2).", el ?? root));
        }
        else if (!val.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(Make("UBL-002", "CustomizationID", BusinessRuleSeverity.Warning,
                $"CustomizationID '{val}' — UBL-TR için 'TR' ile başlamalı.", el!));
        }
    }

    // ── UBL-003: ProfileID geçerli değer ──────────────────────────────────
    private static void CheckProfileId(XElement root, List<BusinessRuleResult> results, out string? profileId)
    {
        var el = root.Element(Cbc + "ProfileID");
        profileId = el?.Value?.Trim();
        if (string.IsNullOrEmpty(profileId))
        {
            results.Add(Make("UBL-003", "ProfileID", BusinessRuleSeverity.Error,
                "ProfileID eksik.", el ?? root));
            return;
        }
        if (!ValidProfileIds.Contains(profileId))
        {
            results.Add(Make("UBL-003", "ProfileID", BusinessRuleSeverity.Error,
                $"ProfileID '{profileId}' geçerli değil. Beklenen: {string.Join(", ", ValidProfileIds)}.", el!));
        }
    }

    // ── UBL-004: InvoiceTypeCode geçerli değer ─────────────────────────────
    private static void CheckInvoiceTypeCode(XElement root, List<BusinessRuleResult> results, out string? code)
    {
        var el = root.Element(Cbc + "InvoiceTypeCode");
        code = el?.Value?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            results.Add(Make("UBL-004", "InvoiceTypeCode", BusinessRuleSeverity.Error,
                "InvoiceTypeCode eksik.", el ?? root));
            return;
        }
        if (!ValidInvoiceTypeCodes.Contains(code))
        {
            results.Add(Make("UBL-004", "InvoiceTypeCode", BusinessRuleSeverity.Error,
                $"InvoiceTypeCode '{code}' geçerli değil. Beklenen: {string.Join(", ", ValidInvoiceTypeCodes)}.", el!));
        }
    }

    // ── UBL-005: ProfileID + InvoiceTypeCode uyumu ─────────────────────────
    private static void CheckProfileInvoiceTypeCombo(
        string? profileId, string? code, XElement root, List<BusinessRuleResult> results)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(code)) return;

        // IHRACAT sadece SATIS ile birleşmeli
        if (profileId.Equals("IHRACAT", StringComparison.OrdinalIgnoreCase)
            && !code.Equals("SATIS", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(Make("UBL-005", "Profil/Tür Uyumu", BusinessRuleSeverity.Error,
                $"ProfileID='IHRACAT' yalnızca InvoiceTypeCode='SATIS' ile kullanılabilir (şu an '{code}').",
                root.Element(Cbc + "InvoiceTypeCode")!));
        }

        // YOLCUBERABERFATURA sadece SATIS veya IADE
        if (profileId.Equals("YOLCUBERABERFATURA", StringComparison.OrdinalIgnoreCase)
            && !(code.Equals("SATIS", StringComparison.OrdinalIgnoreCase)
                 || code.Equals("IADE", StringComparison.OrdinalIgnoreCase)))
        {
            results.Add(Make("UBL-005", "Profil/Tür Uyumu", BusinessRuleSeverity.Error,
                $"ProfileID='YOLCUBERABERFATURA' yalnızca SATIS veya IADE ile kullanılabilir (şu an '{code}').",
                root.Element(Cbc + "InvoiceTypeCode")!));
        }
    }

    // ── UBL-006: Fatura ID formatı (3 harf + 13 rakam) ─────────────────────
    private static void CheckInvoiceId(XElement root, List<BusinessRuleResult> results)
    {
        var el = root.Element(Cbc + "ID");
        var val = el?.Value?.Trim();
        if (string.IsNullOrEmpty(val))
        {
            results.Add(Make("UBL-006", "Fatura No", BusinessRuleSeverity.Error,
                "Fatura ID (numarası) eksik.", el ?? root));
            return;
        }
        if (!InvoiceIdRegex.IsMatch(val))
        {
            results.Add(Make("UBL-006", "Fatura No", BusinessRuleSeverity.Warning,
                $"Fatura No '{val}' UBL-TR formatına uymuyor (3 büyük harf + 13 rakam, örn. ABC2025000000001).", el!));
        }
    }

    // ── UBL-007: UUID GUID formatında ──────────────────────────────────────
    private static void CheckUuid(XElement root, List<BusinessRuleResult> results)
    {
        var el = root.Element(Cbc + "UUID");
        var val = el?.Value?.Trim();
        if (string.IsNullOrEmpty(val))
        {
            results.Add(Make("UBL-007", "UUID", BusinessRuleSeverity.Error,
                "UUID elementi eksik.", el ?? root));
            return;
        }
        if (!Guid.TryParse(val, out _))
        {
            results.Add(Make("UBL-007", "UUID", BusinessRuleSeverity.Error,
                $"UUID '{val}' geçerli bir GUID değil.", el!));
        }
    }

    // ── UBL-008: IssueDate yyyy-MM-dd formatında ───────────────────────────
    private static void CheckIssueDate(XElement root, List<BusinessRuleResult> results)
    {
        var el = root.Element(Cbc + "IssueDate");
        var val = el?.Value?.Trim();
        if (string.IsNullOrEmpty(val))
        {
            results.Add(Make("UBL-008", "Düzenleme Tarihi", BusinessRuleSeverity.Error,
                "IssueDate elementi eksik.", el ?? root));
            return;
        }
        if (!IssueDateRegex.IsMatch(val)
            || !DateTime.TryParseExact(val, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out _))
        {
            results.Add(Make("UBL-008", "Düzenleme Tarihi", BusinessRuleSeverity.Error,
                $"IssueDate '{val}' — yyyy-MM-dd formatında olmalı.", el!));
        }
    }

    // ── UBL-009: DocumentCurrencyCode 3 karakter ISO 4217 ──────────────────
    private static void CheckCurrencyCode(XElement root, List<BusinessRuleResult> results)
    {
        var el = root.Element(Cbc + "DocumentCurrencyCode");
        var val = el?.Value?.Trim();
        if (string.IsNullOrEmpty(val))
        {
            results.Add(Make("UBL-009", "Para Birimi", BusinessRuleSeverity.Error,
                "DocumentCurrencyCode eksik.", el ?? root));
            return;
        }
        if (val.Length != 3 || !val.All(char.IsLetter))
        {
            results.Add(Make("UBL-009", "Para Birimi", BusinessRuleSeverity.Error,
                $"DocumentCurrencyCode '{val}' ISO 4217 (3 harf) olmalı (örn. TRY, USD).", el!));
        }
    }

    // ── UBL-010/011: Taraf VKN/TCKN varlığı ───────────────────────────────
    private static void CheckPartyIdentification(
        XElement root, string partyLocalName, string ruleId, string partyLabel,
        List<BusinessRuleResult> results, bool requireTaxId)
    {
        var partyEl = root.Element(Cac + partyLocalName)?.Element(Cac + "Party");
        if (partyEl is null)
        {
            var sev = requireTaxId ? BusinessRuleSeverity.Error : BusinessRuleSeverity.Warning;
            results.Add(Make(ruleId, $"{partyLabel} Bilgisi", sev,
                $"{partyLabel} Party bilgisi eksik.", root));
            return;
        }

        var ids = partyEl.Elements(Cac + "PartyIdentification")
            .Select(p => p.Element(Cbc + "ID"))
            .Where(id => id is not null)
            .ToList();

        var taxIdNode = ids.FirstOrDefault(id =>
        {
            var scheme = id!.Attribute("schemeID")?.Value;
            return scheme is not null &&
                   (scheme.Equals("VKN", StringComparison.OrdinalIgnoreCase)
                    || scheme.Equals("TCKN", StringComparison.OrdinalIgnoreCase));
        });

        if (taxIdNode is null)
        {
            var sev = requireTaxId ? BusinessRuleSeverity.Error : BusinessRuleSeverity.Warning;
            results.Add(Make(ruleId, $"{partyLabel} VKN/TCKN", sev,
                $"{partyLabel} için VKN veya TCKN kimlik bilgisi tanımlanmamış.", partyEl));
        }
    }

    // ── UBL-012/013: VKN (10) / TCKN (11) checksum ────────────────────────
    private static void CheckTaxIdChecksums(XElement root, List<BusinessRuleResult> results)
    {
        foreach (var partyName in new[] { "AccountingSupplierParty", "AccountingCustomerParty" })
        {
            var partyEl = root.Element(Cac + partyName)?.Element(Cac + "Party");
            if (partyEl is null) continue;

            var label = partyName == "AccountingSupplierParty" ? "Satıcı" : "Alıcı";

            foreach (var pid in partyEl.Elements(Cac + "PartyIdentification"))
            {
                var idEl = pid.Element(Cbc + "ID");
                var scheme = idEl?.Attribute("schemeID")?.Value;
                var value = idEl?.Value?.Trim();
                if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(scheme)) continue;

                if (scheme.Equals("VKN", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.Length != 10 || !value.All(char.IsDigit))
                    {
                        results.Add(Make("UBL-012", $"{label} VKN", BusinessRuleSeverity.Error,
                            $"VKN '{value}' 10 haneli rakam olmalı.", idEl!));
                    }
                    else if (!IsValidVkn(value))
                    {
                        results.Add(Make("UBL-012", $"{label} VKN", BusinessRuleSeverity.Warning,
                            $"VKN '{value}' algoritmik olarak geçersiz.", idEl!));
                    }
                }
                else if (scheme.Equals("TCKN", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.Length != 11 || !value.All(char.IsDigit))
                    {
                        results.Add(Make("UBL-013", $"{label} TCKN", BusinessRuleSeverity.Error,
                            $"TCKN '{value}' 11 haneli rakam olmalı.", idEl!));
                    }
                    else if (!IsValidTckn(value))
                    {
                        results.Add(Make("UBL-013", $"{label} TCKN", BusinessRuleSeverity.Warning,
                            $"TCKN '{value}' algoritmik olarak geçersiz.", idEl!));
                    }
                }
            }
        }
    }

    // ── UBL-014: En az bir InvoiceLine ────────────────────────────────────
    private static void CheckInvoiceLinesExist(XElement root, List<BusinessRuleResult> results, out int count)
    {
        count = root.Elements(Cac + "InvoiceLine").Count();
        if (count == 0)
        {
            results.Add(Make("UBL-014", "Fatura Satırı", BusinessRuleSeverity.Error,
                "Faturada en az bir InvoiceLine bulunmalı.", root));
        }
    }

    // ── UBL-015: KDV tutarlılığı (LegalMonetaryTotal + TaxTotal) ──────────
    private static void CheckMonetaryTotals(XElement root, List<BusinessRuleResult> results)
    {
        var lmt = root.Element(Cac + "LegalMonetaryTotal");
        if (lmt is null)
        {
            results.Add(Make("UBL-015", "Mali Toplamlar", BusinessRuleSeverity.Error,
                "LegalMonetaryTotal elementi eksik.", root));
            return;
        }

        var lineExt = ParseDecimal(lmt.Element(Cbc + "LineExtensionAmount")?.Value);
        var taxExcl = ParseDecimal(lmt.Element(Cbc + "TaxExclusiveAmount")?.Value);
        var taxIncl = ParseDecimal(lmt.Element(Cbc + "TaxInclusiveAmount")?.Value);
        var payable = ParseDecimal(lmt.Element(Cbc + "PayableAmount")?.Value);

        var totalTax = root.Elements(Cac + "TaxTotal")
            .Select(t => ParseDecimal(t.Element(Cbc + "TaxAmount")?.Value) ?? 0m)
            .Sum();

        if (lineExt.HasValue && taxIncl.HasValue)
        {
            var expected = lineExt.Value + totalTax;
            if (Math.Abs(expected - taxIncl.Value) > 0.02m)
            {
                results.Add(Make("UBL-015", "KDV Tutarlılığı", BusinessRuleSeverity.Error,
                    $"TaxInclusiveAmount ({taxIncl:F2}) ≠ LineExtensionAmount ({lineExt:F2}) + TaxTotal ({totalTax:F2}) = {expected:F2}.",
                    lmt.Element(Cbc + "TaxInclusiveAmount")!));
            }
        }

        if (taxExcl.HasValue && lineExt.HasValue && Math.Abs(taxExcl.Value - lineExt.Value) > 0.02m)
        {
            results.Add(Make("UBL-015", "KDV Tutarlılığı", BusinessRuleSeverity.Warning,
                $"TaxExclusiveAmount ({taxExcl:F2}) ≠ LineExtensionAmount ({lineExt:F2}).",
                lmt.Element(Cbc + "TaxExclusiveAmount")!));
        }

        if (payable.HasValue && taxIncl.HasValue && Math.Abs(payable.Value - taxIncl.Value) > 0.02m)
        {
            // PayableAmount; indirim/artırım ve önden ödeme varsa farklı olabilir — Info seviyesi
            results.Add(Make("UBL-015", "Ödenecek Tutar", BusinessRuleSeverity.Info,
                $"PayableAmount ({payable:F2}) TaxInclusiveAmount ({taxIncl:F2}) ile eşleşmiyor. İndirim/ön ödeme varsa normal olabilir.",
                lmt.Element(Cbc + "PayableAmount")!));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static BusinessRuleResult Make(
        string ruleId, string ruleName, BusinessRuleSeverity severity, string message, XElement el)
    {
        var li = (IXmlLineInfo)el;
        return new BusinessRuleResult(
            ruleId, ruleName, severity, message,
            li.HasLineInfo() ? li.LineNumber : null,
            li.HasLineInfo() ? li.LinePosition : null);
    }

    private static decimal? ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>Türk Gelir İdaresi VKN 10 haneli checksum algoritması.</summary>
    private static bool IsValidVkn(string vkn)
    {
        if (vkn.Length != 10 || !vkn.All(char.IsDigit)) return false;
        var digits = vkn.Select(c => c - '0').ToArray();
        long sum = 0;
        for (int i = 0; i < 9; i++)
        {
            long tmp = (digits[i] + (9 - i)) % 10;
            sum += tmp == 0 ? tmp : (tmp * (long)Math.Pow(2, 9 - i)) % 9;
        }
        long expected = (10 - (sum % 10)) % 10;
        return expected == digits[9];
    }

    /// <summary>Nüfus ve Vatandaşlık İşleri TCKN 11 haneli checksum algoritması.</summary>
    private static bool IsValidTckn(string tckn)
    {
        if (tckn.Length != 11 || !tckn.All(char.IsDigit)) return false;
        var d = tckn.Select(c => c - '0').ToArray();
        if (d[0] == 0) return false;

        int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        int evenSum = d[1] + d[3] + d[5] + d[7];
        int tenth = ((oddSum * 7) - evenSum + 10) % 10;
        int eleventh = (d.Take(10).Sum()) % 10;

        return tenth == d[9] && eleventh == d[10];
    }
}
