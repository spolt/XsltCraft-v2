# Plan — Şablonlarım: A5 (sayfalı / çift kopya) şablonlarda sabit not

> Durum: **uygulandı (2.1.4, 2026-11-17)** · rev. 3 — tablo bazlı, K1–K3 kapandı · Sahip hız önceliğiyle architect adımı atlandı; W1+W2 tek oturumda, kapı: reviewer + security-reviewer · Tarih: 2026-11-17 · Kapsam: Şablonlarım → Sabit Not Ekle (`FixedNoteInjector`, `POST /api/user-xslt-templates/bulk-add-note`)

## Sorun
Sahibin paylaştığı A5 e-Fatura şablonunda "Sabit Not Ekle" sonucu **Not bölümü yok** (`no_notes`).

Şablonun yapısı (A5 tasarımlarda yaygın):
- `match="/"` → `xsl:call-template name="Paging"`; `Paging` kendini `index+1` ile çağıran **özyinelemeli sayfalama** şablonu.
- Her sayfada **iki kopya** (iki `xsl:for-each select="$XML"` bloğu, `$XML` = `/`). Her kopyada aynı `table id="notesTableKck"`.
- Not tablosu yalnız **son sayfada**: `xsl:if test="$index = $count"`.
- `notesTableKck` içinde farklı satırlar bir arada: Müşteri No (`PartyIdentification` döngüsü), **genel not döngüsü** (`cbc:Note[not(contains(…)) and …]`), KDV muafiyet nedeni, ödeme açıklaması/koşulu/şekli, sabit IBAN satırı.

**`cbc:Note` şablonda başka yerlerde de geçiyor — bunlar "Notlar" değildir:**

| Yer | Kullanım | Neden not alanı değil |
|---|---|---|
| Üst düzey `xsl:variable` (`odemeTipi`, `musteriKisaAd`, `sevk…`) | `for-each //n1:Invoice/cbc:Note` + `choose` | Değer çıkarımı, çıktıya basılmaz |
| Fatura bilgileri tablosu (`invoiceDetailTblKck`) | `for-each cbc:Note[contains(.,'!#INTERNET#')]` (`xsl:if` içinde) | **Seçici** döngü: belirli kodlu notu ayrıştırıp alanlara böler |
| Fatura bilgileri | `xsl:if test="…cbc:Note[contains(.,'Satış Temsilcisi:')]"` + `substring-after` | Tek değer okuma |
| Müşteri kutusu | `substring-after(…cbc:Note[contains(.,'Müşteri Açıklama :')], …)` | Tek değer okuma |
| **`notesTableKck`** | `for-each cbc:Note[not(contains(.,'!#')) and not(…) …]` | **Gerçek Notlar bölümü** (yalnız dışlayıcı filtre = "kodlu olmayan tüm notlar") |

Hedef: yalnız gerçek Notlar tablosunu bulmak ve **her kopyanın not tablosuna tam bir satır** eklemek.

## Kök neden (koda karşı doğrulandı)
Hedef `XsltNoteLoops.FindUnconditionalNoteLoop` ile seçiliyor (`backend/XsltCraft.Application/Xslt/XsltNoteLoops.cs`):

| # | Bugünkü kural | Örnek şablonda | Sonuç |
|---|---|---|---|
| 1 | `select` tam olarak `//n1:Invoice/cbc:Note` | genel döngüde dışlayıcı yüklem var | eşleşmez |
| 2 | Atada `if/when/choose` olmamalı | `$index <= $count` (sayfa bekçisi) + `$index = $count` (son sayfa) | dışlanır |
| 3 | Tek hedef, tek işaret bloğu | 2 kopya → 2 not tablosu | alt kopya notsuz kalır |
| 4 | Döngü bazlı düşünce | Yalnız #1 gevşetilseydi `!#INTERNET#` gibi seçici döngüler de aday olurdu | yanlış yere ekleme riski |

`FixedNoteInjector` ek kusurları: snippet türetiminde iç `xsl:if` (`not(starts-with(.,'Sevk'))…`) yalnız gövdenin tek çocuğuysa açılıyor → döngü dışında tüm belge metni üzerinde çalışıyor; bitiş ofseti ham metin taramasıyla (yorum içindeki for-each'ler sayımı bozabilir).

## Çözüm: tablo bazlı Notlar bölümü algılama

### 1. Aday not döngüleri ve sınıflandırma (`NoteLoopClassifier`)
Her `xsl:for-each` için:
- **Yol:** son adım `CommonBasicComponents-2` ad alanında `Note`, kök adım `Invoice-2` / `DespatchAdvice-2` (önek değil **namespace URI**, for-each kapsamındaki bildirimlerle). Bağlam ön eki `//`, `/`, göreli ya da kök değişkeni (`$XML/…`, değişken `select="/"`). Kök adımında yüklem → red. `InvoiceLine/cbc:Note` gibi satır notları zaten eşleşmez.
- **Yüklem türü** (mevcut `Validation/Safety/XPathTokenizer` ile — tırnak içindeki `'[Merkez'`, `'[RuhsatNo]'` sorun çıkarmaz):
  - *Genel*: yüklem yok, ya da yalnız `not(contains(.,…))` / `not(starts-with(.,…))` / `. != '…'` terimlerinin `and` birleşimi (dışlayıcı filtre), ya da konum yüklemi (`[position()>1]`).
  - *Seçici*: pozitif `contains`/`starts-with`/`=` içeren yüklem (ör. `contains(.,'!#INTERNET#')`) → **Notlar bölümü sayılmaz**.
- **Bağlam** (sert red): atada `variable, param, with-param, attribute, comment, message, sort, key, result-document`; gizli/işlevsel kapsayıcı (`head, title, script, style, noscript, textarea, option`, `id="qrvalue"`, satır içi `display:none` / `visibility:hidden`).
- **Koşullar:** atadaki `xsl:if`/`xsl:when`/`xsl:otherwise` testleri **görünürlük-nötr** olmalı (aşağıda); `xsl:for-each` ataları kök bağlamı (`/`, `$XML`, yüklemsiz kök eleman) ya da nötr (`1 to 2` kopya döngüsü); içeren şablon `match="/"`/kök eleman ya da dışarıdan `call-template` ile çağrılan adlı şablon (ölü şablon yok).

**Görünürlük-nötr test:** belge yolu/bağlam (`n1:`/`cbc:` adımları, `/`, `.`, `@`) ve belge fonksiyonu (`document, doc, key, id, current, position, last` …) içermez; `$değişken`ler (a) içeren adlı şablonun parametresi (`$index`, `$count`), (b) tanımı sabit/nötr değişken (`linePerPage`), (c) sayfa sayısı ifadesi (`count(…InvoiceLine)` + aritmetik) olmalı. Çözümleyici: derinlik tavanı 8, döngü koruması, memo.
→ Kabul: `$index <= $count`, `$index = $count`, `true()`. Red: `…cbc:Note[contains(.,'!#INTERNET#')]`, `cbc:ProfileID='SGK'`, belgeden hesaplanan `$isSGK`.

### 2. Notlar kapsayıcısı (`NotesSectionLocator`)
Her uygun **genel** döngünün kapsayıcısı: en yakın literal `table` atası (yoksa en yakın `td`/`div`/`ul`). Kapsayıcılar puanlanır:

| Sinyal | Puan |
|---|---|
| Kapsayıcı içinde genel not döngüsü (zorunlu ön koşul) | taban |
| `id`/`class` not adlandırması (`note`, `notes`, `notlar`, `aciklama`/`açıklama`, `remark`; ör. `notesTable`, `notesTableKck`, `notesTableTd`) | + |
| İçinde Notlar bölümüne özgü komşu satırlar (`TaxExemptionReason`, `PaymentTerms/cbc:Note`, `PaymentMeans`, "Not"/"Açıklama" etiketleri) | + |
| Belge bilgisi imzası (`cbc:UUID`, `cbc:IssueDate`, `cbc:ID` başlık alanları — ör. `invoiceDetailTbl…`) | − |
| Kalem tablosu (`InvoiceLine`/`DespatchLine` döngüsü) ya da toplamlar (`PayableAmount`) içeriyor | red |

- En yüksek puanlı kapsayıcı **Notlar bölümü**dür. **Ekleme noktası (K1): tablonun en başı** — kapsayıcının içerik başı (`InsertionPointResolver` `ContentStart` mantığı); `tbody` varsa onun başı; ilk satır başlık satırıysa (`th` içeren `tr` / `thead`) onun hemen ardı. Örnekte Müşteri No döngüsünün üstü. Kapsayıcı `table` değil de `td`/`div` ise yine içerik başı.
- Satırın stili yine genel not döngüsünün gövdesinden türetilir (§4); konum değişse de görünüm şablonun not satırlarıyla aynıdır.
- **Yedek:** uygun genel döngü yok ama not adlandırmalı, görünür bir tablo var ve içinde `cbc:Note` başvurusu bulunuyorsa → aynı kuralla tablonun başına satır (stil: bağlama duyarlı varsayılan, §4).
- **Belirsizlik (K2):** farklı yapıda iki (veya daha çok) kapsayıcı aynı en yüksek puanı alırsa şablon değiştirilmez, sonuç `ambiguous` döner; kullanıcı onaylarsa belge sırasında **önce gelen** aday (ve onun kopyaları) seçilerek eklenir (§7).
- Davranış değişikliği: konum artık "döngünün altı" değil "tablonun başı". GİB standart şablonunda da not, `notesTable`'ın başına (mevcut notların üstüne) girer; mevcut testlerdeki konum beklentileri buna göre güncellenir.

### 3. Kopyalar: her kopyaya tam bir satır
Kopya = seçilen Notlar kapsayıcısıyla **aynı `id`'ye** (örnekte iki `notesTableKck`) ya da id yoksa aynı yapısal imzaya (aynı şablon, ata eleman-adı zinciri, aynı genel not yolu) sahip kapsayıcı. Her kopya kapsayıcısına **bir** blok eklenir; seçilmeyen not kullanımlarına (değişkenler, `!#INTERNET#`, Satış Temsilcisi, Müşteri Açıklama) dokunulmaz. Aynı kopyayı iki kez `call-template` ile basan şablonlarda kaynakta tek tablo vardır → tek blok, iki kez render.

### 4. Snippet (`FixedNoteSnippet`, enjektörden ayrılır)
- Stil genel not döngüsünün gövdesinden türetilir. Değer yer-tutucusu: `xsl:value-of` select ∈ `.`, `string(.)`, `normalize-space(.)`, `normalize-space()`, `text()`, `current()`.
- Yer-tutucunun koşul ataları açılır (`xsl:if` → çocukları; `xsl:choose` → ilgili dal). Örnek sonuç: `<tr><td class="capitalKck"><xsl:text>NOT</xsl:text></td></tr>`.
- Yer-tutucu yoksa bağlama duyarlı varsayılan: `table/tbody` → `<tr><td>…</td></tr>`, `tr` → `<td>`, `ul/ol` → `<li>`, diğer → bugünkü `<b>Not: </b>…<br/>`.
- Not metni yalnız `xsl:text` text node (otomatik kaçış — değişmez).

### 5. Enjektör (`FixedNoteInjector`)
- Tüm `xc:fixed-note` blokları sökülür (tavan 16; aşarsa `Failed`), her kopyaya ayrı blok. Ofset `XsltSourceMap.Span(…)` ile (XmlReader tabanlı, yorum/CDATA güvenli); sondan başa ekleme. (`XsltSourceMap` `ThemeSlots/Source`'ta — architect aşamasında `Xslt/Source`'a taşınabilir.)
- `Replace`: her kopyada yeni tek satır. `Append`: eski blok sayısı = kopya sayısı → sırayla eşle; değilse ilk eski bloğun içeriği her kopyada korunur.
- Yanlış yerdeki eski bloklar sökülüp doğru yere taşınır (kendini onarma korunur).
- `IFixedNoteInjector` sözleşmesi değişmez; `FixedNoteResult.Targets` (kopya sayısı) eklenir.
- `FindUnconditionalNoteLoop` (Tema Kullan / Kolay düzenle) **değişmez**.

### 6. Son koşul kalkanı (fail-closed)
Sonuç `SecureXslt.ReaderSettings()` ile yeniden ayrıştırılır: blok sayısı = kopya sayısı; her blok seçilen Notlar kapsayıcısının içinde ve kopya başına tam bir tane; ardından `XsltSafety.FindThreat`. Tutmazsa `Failed`, şablon değişmez.

### 7. Belirsiz şablonlarda kullanıcı onayı (K2)
- **Backend:** `FixedNoteStatus.Ambiguous` + `FixedNoteMode`'dan bağımsız `FixedNoteAmbiguity { Ask, PickFirst }` parametresi (varsayılan `Ask`). `Ask` → şablon değişmez, sonuç `ambiguous` (aday sayısı ile). `PickFirst` → belge sırasındaki ilk aday + kopyaları.
- **API:** `BulkAddFixedNoteRequest.ResolveAmbiguous` (`"first"` | yok). Öğe durumu `"ambiguous"` (`BulkAddFixedNoteItemResult`). IDOR/kilit/Pro kontrolleri aynen.
- **Frontend:** `BulkNoteResultDialog` `ambiguous` satırlarını "Onay gerekli — birden fazla not alanı bulundu" ile listeler; **"İlk bulunan not alanına ekle"** düğmesi yalnız bu şablon id'leriyle aynı not metni/modla `ResolveAmbiguous: "first"` isteği atar; sonuç pencerede güncellenir (Önizle düğmesi dahil). Vazgeçerse şablonlar değişmeden kalır.

## Bilinen etki / sınırlar
- Not yalnız son sayfada (şablonun Notlar tablosu gibi), her kopyada bir kez.
- Örnekte not hücresi `td.capitalKck` (`text-transform:capitalize`) → sabit not kelime başları büyük görünür (şablonun not stili).
- Sayfalı A5'te kaynakta 2 blok olur (kopya başına 1); Notlar tablosu yalnız son sayfada basıldığı için çok sayfalı faturada da çıktıda toplam 2 satır görünür (son sayfanın iki kopyasında birer).
- Kapsam dışı: notları `apply-templates match="cbc:Note"` ile basan şablonlar; Notlar tablosunu yalnız "not varsa" koşuluyla açan şablonlar (içerik koşulu → `no_notes`); **Kolay düzenle (K3 — bu plana dahil değil)**.

## Görevler
**W1 — Backend (`backend/XsltCraft.Application/Xslt/`):**
- B1 `NoteLoops/NoteLoopClassifier.cs` — yol eşleme (namespace URI), genel/seçici yüklem, bağlam/koşul uygunluğu (nötr test çözümleyici).
- B2 `NoteLoops/NotesSectionLocator.cs` — kapsayıcı puanlama, yedek (adlandırmalı tablo), kopya gruplama → `IReadOnlyList<NotesTarget>` (kapsayıcı, ekleme noktası, stil kaynağı döngü).
- B3 `FixedNoteSnippet.cs` + `FixedNoteInjector.cs` — çoklu blok, tablo başı ekleme noktası, `XsltSourceMap` ofsetleri, Append eşleme, son koşul kalkanı, `Ambiguous`/`PickFirst`.
- B4 `DTO/BulkAddFixedNoteRequest.cs` (`ResolveAmbiguous`) + `UserXsltTemplateController.BulkAddFixedNote` (`ambiguous` durumu).

**W2 — Frontend (K2 için zorunlu):**
- F1 `services/userXsltService.ts`: `FixedNoteItemStatus` += `'ambiguous'`, istek `resolveAmbiguous?: 'first'`.
- F2 `components/storage/BulkNoteResultDialog.tsx`: `ambiguous` satırı + "İlk bulunan not alanına ekle" eylemi; `pages/MyXsltTemplatesPage.tsx`'te yeniden çalıştırma geri çağrısı. **Not:** `MyXsltTemplatesPage.tsx` ve `BulkAddNoteModal.tsx` çalışma ağacında commit edilmemiş toplu-ilerleme değişiklikleri içeriyor — W2'den önce onların commit'lenmesi gerekir.

**W1 — Test (yalnız kritik; `backend/XsltCraft.Application.Tests/Xslt/FixedNoteInjectorTests.cs`):**
- T1 Anonim, kırpılmış A5 fixture (raw string; base64/JS/firma bilgisi — unvan, adres, IBAN, Mersis — **yok**, repo public). Fixture **tuzakları** içermeli: `xsl:variable` içindeki not döngüleri, `invoiceDetailTbl` içindeki `!#INTERNET#` seçici döngüsü, Satış Temsilcisi / Müşteri Açıklama okumaları, `notesTable` içinde Müşteri No döngüsü + genel not döngüsü + KDV/ödeme/IBAN satırları, 2 kopya, `Paging`. Beklenen: `Updated`, **tam 2 blok**, her biri kendi kopyasının `notesTable…` tablosunun **ilk satırı** (Müşteri No döngüsünün üstünde); tuzaklara dokunulmamış; `SecureXslt.Compile` derler; `ThemeSlots/Samples/invoice.xml` ile .NET render'da not metni **tam 2 kez**; snippet'te `starts-with` yok.
- T2 Replace ×2 → 2 blok, eski metin yok; Append → her blokta iki metin.
- T3 Belirsizlik: farklı yapıda iki eşit puanlı Notlar tablosu → `Ask` ile `Ambiguous` ve XSLT değişmez; `PickFirst` ile ilk tabloya eklenir.
- T4 Mevcut 9 test: konum beklentileri "tablonun başı"na göre güncellenir; `Returns_no_notes_section_when_only_conditional_loops_exist` fixture koşulu `true()` → içerik koşulu (`n1:Invoice/cbc:ProfileID='SGK'`) yapılır (niyet korunur).

**Kapı:** `reviewer` + kısa `security-reviewer` (yeni ayrıştırma noktaları `SecureXslt.ReaderSettings()`, çözümleyici/blok tavanları, not metni text node).
**Doküman:** `CHANGELOG.md` [Unreleased] → "Düzeltildi"; sürüm önerisi **2.1.4**.

## Kabul kriterleri
1. Paylaşılan A5 şablon → "Eklendi"; önizlemede **her iki kopyanın** Notlar tablosunun (`notesTableKck`) en başında, **kopya başına tek satır**.
2. Değişkenler, fatura bilgileri (`!#INTERNET#`, Satış Temsilcisi) ve müşteri kutusundaki not kullanımları değişmez.
3. Tekrar "Değiştir" → yine 2 satır; "Ekle" → kopya başına iki satır.
4. A4 şablonlarda not, tespit edilen Notlar tablosunun başına girer; kalem/toplam/belge bilgisi tablolarına asla girmez.
5. Belirsiz şablonda kullanıcıya sorulur; onaysız şablon değişmez, onayda ilk aday (ve kopyaları) kullanılır.
6. Sonuç her zaman iyi-biçimli ve derlenir; değilse "Hata", şablon değişmez. Çıktıya yorum yazılmaz; namespace/`version` değişmez.

## Doğrulama
`dotnet test backend/XsltCraft.Application.Tests --filter "FullyQualifiedName~FixedNoteInjector"`; elle: şablonu Şablonlarım'a yükle → Sabit Not Ekle (Değiştir, sonra Ekle) → 1 sayfalık ve >10 kalemli faturayla önizleme; bir A4 şablonda regresyon.

## Sahip kararları
| # | Karar | Durum / Önerilen | Alternatif |
|---|---|---|---|
| — | Çoklu kopya | **Karar (2026-11-17):** gerçek Notlar tablosu; A5'teki her iki kopyaya birer satır | — |
| K1 | Tablo içindeki konum | **Karar (2026-11-17):** tablonun en başı (başlık satırı varsa ardı) | — |
| K2 | Belirsizlik | **Karar (2026-11-17):** kullanıcıya sor; kabul ederse belge sırasında önce gelen aday | — |
| K3 | Kolay düzenle | **Karar (2026-11-17):** bu plana dahil değil | — |
