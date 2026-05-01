---
id: supplier-party-address
triggers: [satıcı, supplier, adres, address, telefon, faks, web, websiteuri, e-posta, elektronik, vergi dairesi, kimlik, mersisno, ticaretsicil, postaladdress]
---

## Pattern: Satıcı Adres ve İletişim Bilgileri (AccountingSupplierParty)

Temel path: n1:Invoice/cac:AccountingSupplierParty/cac:Party

Bu blok altında 6 bağımsız bilgi grubu bulunur. Biri kaldırılınca diğerleri etkilenmez.

### 1. Adres (PostalAddress)

Path: cac:Party/cac:PostalAddress
Alan sırası: cbc:StreetName → cbc:BuildingName → cbc:BuildingNumber (No:) → cbc:Room (Kapı No:)
  → cbc:PostalZone → cbc:CitySubdivisionName → cbc:CityName

Belirli bir alan gizlenecekse → ilgili xsl:for-each veya xsl:if bloğunu sil.
Sadece Şehir ve İlçe gösterilecekse → StreetName/BuildingName/BuildingNumber/Room bloklarını sil.
Adres tamamen gizlenecekse → xsl:for-each select="cac:PostalAddress" bloğunu içiyle kaldır.

### 2. Telefon ve Faks (Contact)

Path: cac:Party/cac:Contact/cbc:Telephone ve cbc:Telefax
Blok xsl:if ile sarılıdır: test="...cbc:Telephone or ...cbc:Telefax"

Sadece telefon → cbc:Telefax bloğunu ve xsl:if'teki "or ...Telefax" koşulunu sil.
Sadece faks → cbc:Telephone bloğunu ve xsl:if'teki "or ...Telephone" koşulunu sil.
İkisi de gizlenecekse → tüm <tr> bloğunu (dış xsl:if dahil) kaldır.

### 3. Web Sitesi (WebsiteURI)

Path: cac:Party/cbc:WebsiteURI — kendi <tr>'sinde xsl:for-each ile sarılıdır.
Gizlemek için → xsl:for-each select="//n1:Invoice/.../cbc:WebsiteURI" bloğunu <tr> ile kaldır.

### 4. E-Posta (Contact/ElectronicMail)

Path: cac:Party/cac:Contact/cbc:ElectronicMail — kendi <tr>'sinde xsl:for-each ile sarılıdır.
Gizlemek için → xsl:for-each select="//n1:Invoice/.../cbc:ElectronicMail" bloğunu <tr> ile kaldır.

### 5. Vergi Dairesi (PartyTaxScheme)

Path: cac:Party/cac:PartyTaxScheme/cac:TaxScheme/cbc:Name
Sabit "Vergi Dairesi:" etiketiyle basılır. Veri yoksa etiket yine görünür (tasarım kararı).
Gizlemek için → xsl:for-each select="cac:PartyTaxScheme" bloğunu ve etiket <td>'sini kaldır.

### 6. Kimlik Numaraları (PartyIdentification)

Path: cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='...']
Her kayıt ayrı <tr> oluşturur. schemeID → etiket eşlemesi:
  'MERSISNO' → "Mersis No:", 'TICARETSICILNO' → "Ticaret Sicil No:"
  'VKN'/'TCKN' ve diğerleri → xsl:otherwise ile schemeID değeri etiket olur.

Belirli bir tip gizlenecekse → ilgili xsl:when bloğunu sil;
  xsl:otherwise'a düşmesini engellemek için: <xsl:if test="cbc:ID/@schemeID != 'MERSISNO'">
Sadece VKN gösterilecekse → xsl:choose'u kaldır, şununla değiştir:
  <xsl:if test="cbc:ID/@schemeID = 'VKN'"><td>VKN: <xsl:value-of select="cbc:ID"/></td></xsl:if>
Tümü gizlenecekse → xsl:for-each select="...cac:PartyIdentification" bloğunu <tr> ile kaldır.

Kurallar:
- 6 grup birbirinden bağımsızdır.
- Dış xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party" path'ini değiştirme.
- WebsiteURI ve ElectronicMail path'leri // ile başlar; değiştirme.
- Bu pattern SADECE AccountingSupplierParty içindir.
