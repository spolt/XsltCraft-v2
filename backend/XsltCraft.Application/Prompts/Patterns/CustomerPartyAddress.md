---
id: customer-party-address
triggers: [alıcı, customer, müşteri, adres, address, telefon, faks, web, websiteuri, e-posta, elektronik, vergi dairesi, kimlik, postaladdress]
---

## Pattern: Alıcı Adres ve İletişim Bilgileri (AccountingCustomerParty)

Temel path: n1:Invoice/cac:AccountingCustomerParty/cac:Party

Yapı AccountingSupplierParty Adres/İletişim pattern'i ile birebir aynıdır.
Tek fark: path'lerde AccountingSupplierParty yerine AccountingCustomerParty kullanılır.

### 1–6 grupları için SupplierParty pattern'indeki tüm kurallar geçerlidir.

Path farkları:
  Adres    → cac:AccountingCustomerParty/cac:Party/cac:PostalAddress
  Telefon  → cac:AccountingCustomerParty/cac:Party/cac:Contact/cbc:Telephone
  Web      → //n1:Invoice/.../cac:AccountingCustomerParty/.../cbc:WebsiteURI
  E-posta  → //n1:Invoice/.../cac:AccountingCustomerParty/.../cbc:ElectronicMail
  VD       → cac:AccountingCustomerParty/cac:Party/cac:PartyTaxScheme/cac:TaxScheme/cbc:Name
  Kimlik   → cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='...']

Kurallar:
- Dış xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party" path'ini değiştirme.
- Bu pattern SADECE AccountingCustomerParty içindir. SupplierParty ayrıdır.
