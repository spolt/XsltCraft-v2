---
id: invoice-line
triggers: [satır, kolon, sütun, invoiceline, birim fiyat, miktar, ürün kodu, mal hizmet, iskonto, gtip, invoicedquantity, lineextension, yeni kolon, satır ekle]
---

## Pattern: Fatura Satırları Tablosu (InvoiceLine)

İki katmanlıdır — her zaman birlikte ele al:
  KATMAN 1 → Tablo başlık satırı (<tr> içindeki <td> kolonları)
  KATMAN 2 → Satır değerleri (xsl:template match="//n1:Invoice/cac:InvoiceLine")

KURAL: Başlık ve değer her zaman birlikte değiştirilmeli. Sadece başlık kaldırılırsa kolon sayısı uyuşmaz.
TERCİH: Kaldırmak yerine açıklama satırına al:
  Başlık: <!-- <td ...>Kolon Adı</td> -->
  Değer:  <!-- <td class="lineTableTd">...</td> -->

### Kolon Envanteri

Her kolon: [Başlık Koşulu] | [Değer Path'i] | [Tip]

1.  Sıra No — [A]
    Başlık: koşulsuz | Değer: cbc:ID

2.  Ürün Kodu — [B, $varItemCode > 0]
    Değer: cac:Item/cac:SellersItemIdentification/cbc:ID
    Not: değişken XML verisine göre otomatik açılır/kapanır.

3.  Mal Hizmet / İade Edilen Mal Hizmet — [B, fatura tipine bağlı]
    Başlık: not($varfaturatipi='SGK') AND not(InvoiceTypeCode='TEVKIFATIADE') → 'Mal Hizmet'
            InvoiceTypeCode='TEVKIFATIADE' → 'İade Edilen Mal Hizmet'
    Değer: cac:Item/cbc:Name

4.  Miktar — [B, not($varfaturatipi='SGK')]
    Değer: cbc:InvoicedQuantity + @unitCode (xsl:choose ile Türkçe karşılık tablosu)
    Yeni birim eklemek → xsl:when bloğu ekle; mevcut when'lere dokunma.
    Bilinmeyen kod → xsl:otherwise ile ham basılır.

5.  Etiket Fiyatı — [B, $varEtiketFiyati = '1']
    Değer: InvoiceLine/cbc:Note'ta 'ETF:' veya 'ESF:' prefix'i; substring-after ile parse edilir.

6.  Birim Fiyat — [A]
    Değer: cac:Price/cbc:PriceAmount (format-number ile)
    Para birimi: TRL/TRY → 'TL', diğerleri → currencyID değeri

7.  Vade Tarihi — [B, $varVade = '1']
    Değer: InvoiceLine/cbc:Note'ta 'VAD:' prefix'i parse edilir.

8.  İskonto/Arttırım Oranı — [B, $varAllowanceRate > 0]
    Değer: cac:AllowanceCharge/cbc:MultiplierFactorNumeric
    ChargeIndicator=true → '(+) %', false → '(-) %'

9.  İskonto/Arttırım Tutarı — [B, $varAllowanceAmount > 0]
    Değer: cac:AllowanceCharge/cbc:Amount (Curr_Type template ile)

10. İskonto/Arttırım Nedeni — [B, $varAllowanceReason > 0]
    Değer: cac:AllowanceCharge/cbc:AllowanceChargeReason
    ChargeIndicator=true → 'Arttırım - ', false → 'İskonto - '

11. KDV Oranı — [B, not(InvoiceTypeCode='TEVKIFATIADE')]
    Değer: TaxTotal/TaxSubtotal/TaxCategory[TaxScheme/TaxTypeCode='0015']/Percent

12. KDV Tutarı — [B, not(InvoiceTypeCode='TEVKIFATIADE')]
    Değer: TaxTypeCode='0015' → TaxAmount (Curr_Type ile)
    WithholdingTaxTotal varsa → 'KDV TEVKİFAT (%X)=Y' formatında ek satır
    cbc:Note'ta 'AVANS MAHSUBU' veya 'NAKİT TEMİNAT KESİNTİSİ' → ek satır

13. Mal Hizmet Tutarı — [B, not(InvoiceTypeCode='TEVKIFATIADE')]
    Değer: cbc:LineExtensionAmount (Curr_Type ile)

14. TEVKIFATIADE özel kolonları — [B, InvoiceTypeCode='TEVKIFATIADE']
    (İade Edilen Mal Oranı, İadeye Konu KDV, Diğer Vergiler,
     İadeye Konu İşlem Bedeli, Alıştaki Tevkifatsız KDV)
    Özel yapıdır; standart kolon gibi ele alma, ayrıca değerlendir.

15. GTIP — [B, ProfileID='EARSIVFATURA' AND InvoiceTypeCode='ISTISNA']
    Değer: cac:Delivery/cac:Shipment/cac:GoodsItem/cbc:RequiredCustomsID

16. Künye Numarası — [B, ProfileID='HKS' OR InvoiceTypeCode='HKSSATIS/HKSKOMISYONCU']
    Değer: cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='KUNYENO']

17. Mal Sahibi VKN/TCKN ve Ad/Soyad — [B, HKS satış faturası]
    Değer: AdditionalItemIdentification[@schemeID='MALSAHIBIVKNTCKN/MALSAHIBIADSOYADUNVAN']

18. İhracat Kolonları (Teslim Şartı, Eşya Kap Cinsi, Kap No) — [B, ProfileID='IHRACAT'/'OZELFATURA']
    Değer: DeliveryTerms/cbc:ID[@schemeID='INCOTERMS'], PackagingTypeCode (Packaging template), cbc:ID

### Kolon Gizleme

Önerilen: Açıklama satırına al (geri almak tek adım).
Kalıcı kaldırma: Her iki <td>'yi sil; [B] tipindeyse sarmalayıcı xsl:if başka şey içermiyorsa onu da kaldır.
[A] kolonları için kullanıcıya uyarı ver: 'Kaldırmak yerine açıklama satırına almanızı öneririm.'

### Yeni Kolon Ekleme

Eksikse kullanıcıya sor: 1) Kolon başlığı, 2) XML veri path'i

Başlık (tablo başlık <tr>'sine ekle):
  <td id="invoice-line-td" style="width:10%">
    <span style="font-weight:bold;"><xsl:text>Yeni Kolon</xsl:text></span>
  </td>

Değer (template içinde aynı sıraya ekle):
  <td class="lineTableTd" align="right">
    <xsl:text>&#160;</xsl:text>
    <xsl:value-of select="[path]"/>
  </td>

Koşullu kolon → başlık ve değer bloklarının her ikisini de aynı xsl:if ile sar.
Para birimi gereken kolonlar → Curr_Type template: <xsl:call-template name="Curr_Type"/>

### Değişken Tablosu

$varItemCode → SellersItemIdentification olan satır sayısı
$varAllowanceRate → MultiplierFactorNumeric olan satır sayısı
$varAllowanceAmount → AllowanceCharge/Amount olan satır sayısı
$varAllowanceReason → AllowanceChargeReason olan satır sayısı
$varLineExplanation → InvoiceLine/Note olan satır sayısı
$varEtiketFiyati → ETF:/ESF: içeren Note varsa '1'
$varVade → VAD: içeren Note varsa '1'
$varfaturatipi → SGK faturası kontrolü
Bu değişkenler XSLT başında tanımlıdır; manuel müdahale gerekmez.

Genel Kurallar:
- Başlık ve değer her zaman birlikte değiştirilmeli; sıra bozulursa veriler yanlış kolona düşer.
- [B] tipindeki xsl:if/xsl:choose koşullarına dokunma.
- Yeni kolon için path bilgisi kullanıcıdan alınmadan öneri yazma.
- TEVKIFATIADE kolonları özel yapıdır; ayrıca değerlendir.
