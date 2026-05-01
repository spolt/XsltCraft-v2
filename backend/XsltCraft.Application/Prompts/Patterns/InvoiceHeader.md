---
id: invoice-header
triggers: [başlık, header, fatura no, fatura numarası, tarih, senaryo, profileid, tip, invoicetypecode, dönem, irsaliye, sipariş, ödeme, customizationid]
---

## Pattern: Fatura Başlık Bilgileri (Invoice Header)

Her alan bağımsız bir <tr>'dir. Biri kaldırılınca diğerleri etkilenmez.
İki kategori:
  [A] Zorunlu → her zaman <tr> basılır, veri yoksa boş görünür
  [B] Koşullu → xsl:if/for-each ile sarılıdır, veri yoksa <tr> hiç basılmaz

### 1. Özelleştirme No (CustomizationID) — [A]

Path: n1:Invoice/cbc:CustomizationID
Gizlemek → tüm <tr> bloğunu kaldır. Teknik referans; kaldırılması önerilmez.

### 2. Senaryo (ProfileID) — [A]

Path: n1:Invoice/cbc:ProfileID
Gizlemek → tüm <tr> bloğunu kaldır.
UYARI: ProfileID = 'EGIDERPUSULASI' koşulu başka alanları etkiler:
  - 'Fatura No' yerine 'Pusula No' basılır (Bölüm 5)
  - InvoiceTypeCode satırı gizlenir (Bölüm 3)
Bu koşulları başka yerlerde bozma.

### 3. Fatura Tipi (InvoiceTypeCode) — [B]

Path: n1:Invoice/cbc:InvoiceTypeCode
Koşul: not(ProfileID = 'EGIDERPUSULASI')
Gizlemek → xsl:if bloğunu <tr> ile kaldır.
Her zaman göstermek → dış xsl:if koşulunu kaldır, <tr>'yi doğrudan bırak.

### 4. İlave Fatura Tipi (AccountingCost) — [B]

Path: n1:Invoice/cbc:AccountingCost
Gizlemek → xsl:if bloğunu <tr> ile kaldır.
NOT: AccountingCost aynı zamanda Bölüm 8 (InvoicePeriod) koşulunu da tetikler. Kaldırırsan orayı da kontrol et.

### 5. Fatura No / Pusula No (ID) — [A]

Path: n1:Invoice/cbc:ID
Etiket ProfileID'ye göre değişir: 'EGIDERPUSULASI' → 'Pusula No:', diğerleri → 'Fatura No:'
Etiketi değiştirmek → xsl:when/otherwise içindeki xsl:text'i düzenle; xsl:choose yapısını bozma.
Gizlemek → tüm <tr>'yi kaldır. Önerilmez.

### 6. Fatura Tarihi (IssueDate + IssueTime) — [A]

Path: n1:Invoice/cbc:IssueDate — saat: substring(../cbc:IssueTime,1,5)
Saati gizlemek → xsl:text ve IssueTime value-of satırını sil; IssueDate for-each'i koru.
Tarihi gizlemek → tüm <tr>'yi kaldır. Önerilmez.

### 7. Mükellef Bilgileri (AdditionalDocumentReference) — [B]

Path: cac:AdditionalDocumentReference/cbc:DocumentTypeCode
Filtre: text() = 'MUKELLEF_KODU' or 'MUKELLEF_ADI' or 'DOSYA_NO'
Belirli bir tipi gizlemek (örn: DOSYA_NO) → for-each filtreden ilgili text() karşılaştırmasını ve etiket xsl:if bloğunu sil.
Tümünü gizlemek → xsl:for-each bloğunu <tr> ile kaldır.

### 8. Dönem Başlangıcı / Bitişi (InvoicePeriod) — [B]

Path: n1:Invoice/cac:InvoicePeriod/cbc:StartDate ve cbc:EndDate
Koşul: AccountingCost VE InvoicePeriod ikisi birden varsa görünür.
Gizlemek → dış xsl:if bloğunu iki <tr> ile birlikte kaldır.
AccountingCost koşulsuz göstermek → test'i sadece InvoicePeriod varlığına indir:
  test="//n1:Invoice/cac:InvoicePeriod"

### 9. İrsaliye No / Tarihi (DespatchDocumentReference) — [B]

Path: n1:Invoice/cac:DespatchDocumentReference/cbc:ID ve cbc:IssueDate
Her kayıt iki <tr> (No + Tarih) üretir.
Sadece No → İrsaliye Tarihi <tr>'sini sil.
Tümünü gizlemek → xsl:for-each bloğunu <tr>'lerle kaldır.

### 10. Son Ödeme Tarihi (paymentDueDate) — [B]

Path: n1:Invoice/cbc:paymentDueDate (küçük 'p' ile başlar — UBL-TR'ye özgü)
Gizlemek → xsl:if bloğunu <tr> ile kaldır.

### 11. Sipariş No / Tarihi (OrderReference) — [B]

Path: n1:Invoice/cac:OrderReference/cbc:ID ve cbc:IssueDate — her biri ayrı xsl:if ile bağımsız.
Sadece No → Sipariş Tarihi xsl:if bloğunu kaldır.
Tümünü gizlemek → her iki xsl:if bloğunu kaldır.

### 12. Mal Kabul No (ReceiptDocumentReference) — [B]

Path: n1:Invoice/cac:ReceiptDocumentReference/cbc:ID
Gizlemek → xsl:for-each bloğunu <tr> ile kaldır.

### 13. Aracı Kurum Bilgisi (TaxRepresentativeParty) — [B]

Path: n1:Invoice/cac:TaxRepresentativeParty/cac:PartyIdentification/cbc:ID[@schemeID='ARACIKURUMVKN']
VKN ve Ünvan olmak üzere iki <tr> üretir.
Gizlemek → xsl:for-each bloğunu <tr>'lerle kaldır.

### 14. Ödeme Şekli (PaymentMeansCode) — [B]

Path: n1:Invoice/cac:PaymentMeans/cbc:PaymentMeansCode
Değer xsl:call-template name="PaymentMeansCode" ile çözümlenir.
Ham kodu görmek → xsl:call-template yerine: <xsl:value-of select="."/>
Gizlemek → xsl:if bloğunu <tr> ile kaldır.

### 15. Ödeme Tarihi (PaymentDueDate) — [B]

Path: n1:Invoice/cac:PaymentMeans/cbc:PaymentDueDate
Format YYYY-MM-DD → DD-MM-YYYY: substring(.,9,2)-substring(.,6,2)-substring(.,1,4)
Orijinal format → substring işlemlerini kaldır: <xsl:value-of select="."/>
Gizlemek → xsl:if bloğunu <tr> ile kaldır.

Genel Kurallar:
- ProfileID koşullarına (EGIDERPUSULASI) dokunma; birden fazla alanı etkiler.
- AccountingCost hem Bölüm 4'ü hem Bölüm 8'i tetikler; kaldırırsan her ikisini kontrol et.
- [A] alanlarını kaldırmak önerilmez; kullanıcıya uyarı ver.
- [B] alanı neden görünmüyor sorusunda önce XML'deki veriyi kontrol ettir.
