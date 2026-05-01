---
id: legal-monetary-total
triggers: [toplam, total, legalmonetarytotal, kdv, vergi, ödeme, tutar, tevkifat, taxtotal, matrah, ödenecek]
---

## Pattern: Fatura Dip Toplamları (LegalMonetaryTotal / TaxTotal)

Temel path'ler:
  n1:Invoice/cac:LegalMonetaryTotal
  n1:Invoice/cac:TaxTotal/cac:TaxSubtotal
  n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal

Her satır iki <td>'den oluşur: etiket (sol) + değer (sağ). Satırlar bağımsızdır.
Para birimi her değer için Curr_Type template'i ile basılır. TRL/TRY → 'TL'.

### Satır Envanteri

1.  Mal Hizmet Toplam / İadeye Konu İşlem Bedeli — [A]
    not(TEVKIFATIADE) → 'Mal Hizmet Toplam Tutarı' | TEVKIFATIADE → 'İadeye Konu İşlem Bedeli Tutarı'
    Değer: LegalMonetaryTotal/cbc:LineExtensionAmount

2.  Toplam İskonto / Toplam Arttırım — [A]
    Etiket ChargeIndicator'a göre otomatik değişir; dokunma.
    Değer: LegalMonetaryTotal/cbc:AllowanceTotalAmount
    Özel: $varoptik='medikal' ve $varisitmekatilimpayi doluysa → katılım payı değeri TL basılır.

3.  Hesaplanan [Vergi Adı] (%Oran) — [B, not(TEVKIFATIADE), dinamik çok satır]
    Kaynak: TaxTotal/cac:TaxSubtotal döngüsü; her subtotal ayrı <tr>.
    Değer: TaxSubtotal/cbc:TaxAmount | Etiket: TaxScheme/cbc:Name + Percent
    (OZELMATRAH'ta oran gösterilmez)
    NOT: Dinamik üretilir; tek kaydı gizlemek mümkün değil, tümü açılır/kapanır.

4.  İadeye Konu KDV — [B, TEVKIFATIADE]
    Değer: sum(TaxSubtotal[TaxTypeCode=0015]/cbc:TaxAmount)

5.  KDV Matrahı — [B, TaxTypeCode='4171' varsa]
    Değer: sum(TaxSubtotal[TaxTypeCode=0015]/cbc:TaxableAmount)

6.  Tevkifat Dahil Toplam Tutar — [B, TaxTypeCode='4171', 5. satırla birlikte açılır]
    Değer: LegalMonetaryTotal/cbc:TaxInclusiveAmount

7.  Tevkifat Hariç Toplam Tutar — [B, TaxTypeCode='4171', 5. satırla birlikte açılır]
    Değer: LegalMonetaryTotal/cbc:PayableAmount

8.  Hesaplanan KDV Tevkifat (%Oran) — [B, WithholdingTaxTotal varsa, dinamik çok satır]
    Kaynak: WithholdingTaxTotal/cac:TaxSubtotal döngüsü.

9.  Tevkifata Tabi İşlem Tutarı (9015) — [B, sum(TaxSubtotal[9015]/TaxableAmount) > 0]
    Değer: sum(InvoiceLine[TaxTypeCode=9015]/LineExtensionAmount)

10. Tevkifata Tabi İşlem Üzerinden Hes. KDV (9015) — [B, 9. satırla birlikte]
    Değer: sum(TaxSubtotal[TaxTypeCode=9015]/TaxableAmount)

11. Tevkifata Tabi İşlem Tutarı (WithholdingTaxTotal) — [B, satır bazlı WithholdingTaxTotal varsa]
    Değer: sum(InvoiceLine[WithholdingTaxTotal]/LineExtensionAmount)
    NOT: 9. satırla aynı etiketi taşır ama farklı koşul/kaynak. İkisi aynı anda basılabilir; tasarım farkıdır.

12. Tevkifata Tabi İşlem Üzerinden Hes. KDV (WithholdingTaxTotal) — [B, 11. satırla birlikte]
    Değer: sum(WithholdingTaxTotal/TaxSubtotal/TaxableAmount)

13. Vergiler Dahil Toplam Tutar — [A]
    Değer: LegalMonetaryTotal/cbc:TaxInclusiveAmount

14. Toplam Masraflar — [B, HKS komisyoncu faturasında]
    Koşul: (ProfileID='HKS' AND InvoiceTypeCode='KOMISYONCU') OR
           (ProfileID='EARSIVFATURA' AND InvoiceTypeCode='HKSKOMISYONCU')
    Değer: LegalMonetaryTotal/cbc:ChargeTotalAmount

15. Ödenecek Tutar — [A]
    Değer: LegalMonetaryTotal/cbc:PayableAmount

### Satır Gizleme

[A] satırlar için uyarı ver: 'Yasal zorunluluk içerebilir. Açıklama satırına almanızı öneririm.'
Açıklama satırı: <!-- <tr align="right">...</tr> -->
Kalıcı kaldırma: Kullanıcı ısrar ederse tüm <tr>'yi sil; sarmalayıcı xsl:if boşaldıysa onu da kaldır.

### Yeni Toplam Satırı Ekleme

Eksikse kullanıcıya sor: 1) Etiket metni, 2) Değer path'i

Şablon:
  <tr align="right">
    <td />
    <td class="lineTableBudgetTd" width="200px" align="right">
      <span style="font-weight:bold;"><xsl:text>Yeni Etiket</xsl:text></span>
    </td>
    <td class="lineTableBudgetTd" style="width:82px;" align="right">
      <xsl:for-each select="[path]">
        <xsl:call-template name="Curr_Type"/>
      </xsl:for-each>
    </td>
  </tr>

Hesaplamalı değer: format-number([hesaplama], '###.##0,00', 'european') ardından TRL/TRY → 'TL' kontrolü.

### SGK Değişken Tablosu

cbc:Note prefix'lerinden parse edilir; dokunma:
  $vareldenilackatilimpayi → SGK_EIP:
  $varmaasdanilackatilimpayi → SGK_MIP:
  $vareldenmuayenekatilimpayi → SGK_EMP:
  $varmaasmuayenekatilimpayi → SGK_MMP:
  $vareldenrecetekatilimpayi → SGK_ERP:
  $varmaastanrecetekatilimpayi → SGK_MRP:
  $varoptik → SGK_TYP: ('medikal' ise iskonto → katılım payına yönlenir)
SGK fatura toplamı sorusunda önce $varfaturatipi ve $varoptik değerlerini kontrol ettir.

Genel Kurallar:
- [A] satırları kaldırma, açıklama satırına al.
- Dinamik satırlar (3, 8): tek kaydı gizlemek mümkün değil.
- 5-6-7 satırları TaxTypeCode='4171' varlığına bağlıdır; XML'e dokunmadan gizlemek için xsl:if'i açıklama satırına al.
- 9-10 ve 11-12 çiftleri farklı koşullarla açılır; aynı anda ikisi de basılabilir, hata değil.
- Curr_Type template'ini para birimi gereken tüm yeni satırlarda kullan.
- LegalMonetaryTotal path'ini asla değiştirme.
