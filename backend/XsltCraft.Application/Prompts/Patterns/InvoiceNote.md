---
id: invoice-note
triggers: [note, not, prefix, açıklama, etf, esf, vad]
---

## Pattern: Invoice Note Görünürlüğü (//n1:Invoice/cbc:Note)

Kullanıcının XSLT'sinde Note bloğu genellikle şu yapıdadır:

<xsl:for-each select="//n1:Invoice/cbc:Note">
  <xsl:if test="[koşullar]">
    <b>Not: </b><xsl:value-of select="."/><br/>
  </xsl:if>
</xsl:for-each>

xsl:if yoksa tüm Note'lar filtresiz basılıyordur.

### Kullanıcı bir prefix'i GİZLEMEK istiyorsa (örn: 'X')

Mevcut xsl:if varsa → mevcut test attribute'una AND ile ekle:
  and not(starts-with(.,'X'))

Mevcut xsl:if yoksa → xsl:if bloğu oluştur:
  <xsl:if test="not(starts-with(.,'X'))">

### Kullanıcı sadece belirli prefix'i GÖSTERMEK istiyorsa (örn: 'Y')

  <xsl:if test="starts-with(.,'Y')">

not() KULLANMA. Sadece o prefix geçer, diğerleri gizlenir.

### Kullanıcı mevcut bir gizleme koşulunu KALDIRMAK istiyorsa (örn: 'X' artık görünsün)

Mevcut test içindeki not(starts-with(.,'X')) ifadesini sil.
Eğer test'te başka koşul kalmıyorsa xsl:if bloğunu tamamen kaldır.

### Kullanıcı TÜM Note'ları görmek istiyorsa

xsl:if bloğunu tamamen kaldır, for-each içeriğini düz bırak.

Kurallar:
- SADECE xsl:if test attribute'unu değiştir.
- for-each select path'ini ("//n1:Invoice/cbc:Note") asla değiştirme.
- İçerideki HTML yapısını (<b>, <br/>, boşluklar) koru.
- Kullanıcının XSLT'sinde bu blok yoksa önce bul, bulamazsan sor.
- Bu pattern SADECE header seviyesi Note için geçerlidir. InvoiceLine/Note ayrıdır.
