---
id: supplier-party-person
triggers: [satıcı, supplier, accountingsupplierparty, kişi, person, unvan, partyname, firstname, familyname, vkn, tckn]
---

## Pattern: Satıcı Bilgisi (AccountingSupplierParty)

Temel path: n1:Invoice/cac:AccountingSupplierParty/cac:Party

Bu blokta iki farklı kişi tipi bulunur:
- Tüzel kişi → cac:PartyName/cbc:Name (şirket ünvanı)
- Gerçek kişi → cac:Person (Title, FirstName, MiddleName, FamilyName, NameSuffix)

Kullanıcının XSLT'sinde bu bilgiler genellikle tek bir
xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party"
bloğu içinde yer alır.

Not: Bazı XSLT'lerde $varBranchName değişkeni ve VKN schemeID kontrolü
ile sarılı olabilir. Bu sarmalayıcı koşullar iç yapıya dokunmadan korunmalıdır.

### Sadece ünvan (tüzel kişi) gösterilecekse

cac:Person bloğunu (xsl:for-each select="cac:Person" ve içindeki tüm
cbc:Title/FirstName/MiddleName/FamilyName/NameSuffix döngüleri) kaldır.
cac:PartyName bloğunu koru:

  <xsl:if test="cac:PartyName">
    <xsl:value-of select="cac:PartyName/cbc:Name"/>
    <br/>
  </xsl:if>

### Gerçek kişi ad-soyad bilgileri gösterilecekse

cac:Person bloğu mevcutsa dokunma. Yoksa şu yapıyı ekle (PartyName bloğundan sonra):

  <xsl:for-each select="cac:Person">
    <xsl:for-each select="cbc:Title"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:FirstName"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:MiddleName"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:FamilyName"><xsl:apply-templates/><xsl:text>&#160;</xsl:text></xsl:for-each>
    <xsl:for-each select="cbc:NameSuffix"><xsl:apply-templates/></xsl:for-each>
  </xsl:for-each>

### Belirli bir isim alanı gizlenecekse (örn: MiddleName)

İlgili xsl:for-each bloğunu sil. Silinebilir: cbc:Title, cbc:MiddleName, cbc:NameSuffix.
Silinmemesi önerilen: cbc:FirstName, cbc:FamilyName.

### Vergi numarası (VKN/TCKN) gösterilecekse

PartyName/Person bloğuna dahil değildir; ayrı bir path'tedir:
  cac:PartyIdentification/cbc:ID[@schemeID='VKN']
  cac:PartyIdentification/cbc:ID[@schemeID='TCKN']

Mevcut for-each bloğu içine eklemek için:
  <xsl:value-of select="cac:PartyIdentification/cbc:ID[@schemeID='VKN']"/>

Kurallar:
- Dış xsl:for-each select path'ini asla değiştirme.
- $varBranchName ve VKN count() koşulları varsa koru, içine dokunma.
- PartyName ve Person blokları bağımsızdır; biri kaldırılınca diğeri etkilenmez.
- Gerçek kişi faturalarında PartyName boş, Person dolu gelir; ikisini XSLT'de bırakmak en güvenlidir.
- Bu pattern SADECE AccountingSupplierParty içindir.
