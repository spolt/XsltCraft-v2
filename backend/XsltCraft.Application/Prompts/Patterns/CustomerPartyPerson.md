---
id: customer-party-person
triggers: [alıcı, customer, accountingcustomerparty, müşteri, kişi, person, unvan, partyname, firstname, familyname, vkn, tckn]
---

## Pattern: Alıcı Bilgisi (AccountingCustomerParty)

Temel path: n1:Invoice/cac:AccountingCustomerParty/cac:Party

Yapı AccountingSupplierParty ile birebir aynıdır:
- Tüzel kişi → cac:PartyName/cbc:Name
- Gerçek kişi → cac:Person (Title, FirstName, MiddleName, FamilyName, NameSuffix)

Kullanıcının XSLT'sinde genellikle:
xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party"
bloğu içinde yer alır.

### Sadece ünvan gösterilecekse

  <xsl:if test="cac:PartyName">
    <xsl:value-of select="cac:PartyName/cbc:Name"/>
    <br/>
  </xsl:if>

### Gerçek kişi ad-soyad bilgileri gösterilecekse

  <xsl:for-each select="cac:Person">
    <xsl:for-each select="cbc:Title"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:FirstName"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:MiddleName"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:FamilyName"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:NameSuffix"><xsl:apply-templates/></xsl:for-each>
  </xsl:for-each>

### Vergi numarası (VKN/TCKN) gösterilecekse

  cac:PartyIdentification/cbc:ID[@schemeID='VKN']
  cac:PartyIdentification/cbc:ID[@schemeID='TCKN']

Kurallar (SupplierParty ile aynı):
- Dış xsl:for-each select path'ini asla değiştirme.
- PartyName ve Person blokları bağımsızdır.
- Gerçek kişi faturalarında PartyName boş, Person dolu gelir; ikisini XSLT'de bırakmak en güvenlidir.
- Bu pattern SADECE AccountingCustomerParty içindir. SupplierParty ayrıdır.
