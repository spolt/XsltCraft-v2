# Plan — Tema Kullan: Sabit notlar

> Durum: **onaylandı** · Tarih: 2026-10-27 · Kaynak: `planner` agent (kod-gerçeğine karşı doğrulandı)

Tema Kullan (`/theme-use/:id`) ekranında, temanın **Notlar** kutusuna kullanıcının yazdığı sabit metin eklenir; kalın / italik / altı çizili ve font ayarlarıyla.

## Sahip kararları (2026-10-27)
1. **"Notların üstü"** = `cbc:Note` döngüsünün (`XsltNoteLoops.FindUnconditionalNoteLoop`) **hemen öncesi** (`Before`) — istisna/tevkifat satırları varsa notun üstünde kalır. **"Notların altı"** = döngüyü içeren en yakın literal `td`/`th` (yoksa literal ebeveyn) hücresinin **içerik sonu** (`ContentEnd`; Ödeme Koşulu vb. koşullu son satırların da altı).
2. **Notlar alanı bulunamazsa** bölüm pasif kalır ("Bu temada Notlar alanı bulunamadı"); not yanlış yere yazılmaz. İstek yine not içerirse uyarı kodu `notes-not-found`, not uygulanmaz.
3. **Konum başına tek not** (en fazla 2 not: biri `before`, biri `after`). Not çok satırlı olabilir (`\n` → `<br/>`).

## Mevcut durum (doğrulandı)
- Birleştirme: `ThemeAssetComposer.Compose` (`backend/XsltCraft.Application/ThemeSlots/Compose/ThemeAssetComposer.cs`) — eski `DEV-MODE-OVERRIDES` bloğunu siler, görseller/banka bloğunu `</body>` önüne ekler, sonunda `XsltSafety.FindThreat`. Önizleme, indirme ve kaydet aynı yol.
- API: `POST /api/preview/compose` (`ThemeComposeController`, `ThemeComposeDtos`, `ThemeComposeRequestValidator`); `themeId` → depodaki temiz tema, `xslt` → Geliştirici Modu yolu; render `XsltRoute.Theme` (1.0 .NET / 2.0 Saxon worker).
- Konuma göre ekleme altyapısı: `XsltSourceMap.TryBuild`, `ThemeDocument.TryCreate`, `InsertionPointResolver.Resolve(target, ContentStart/ContentEnd/Before)`, `SourceEdits`, `ThemeDocument.LiteralAncestor` (`backend/XsltCraft.Application/ThemeSlots/{Source,Analysis}/*`).
- Özelleştirme saklanmıyor (sayfa state'i); kaydet/indir birleştirilmiş XSLT metnini kullanır.
- Benzer ama ayrı: `FixedNoteInjector` (Şablonlarım toplu sabit not) — **dokunulmaz**; yalnız `XsltNoteLoops` ortak kullanılır.

## Sözleşme (backend ↔ frontend)
İstek (`ThemeComposeRequest`), yeni opsiyonel alan:
```json
"fixedNotes": [
  { "position": "before" | "after",
    "text": "string (≤500 karakter, ≤10 satır, \\n dışında kontrol karakteri yok)",
    "bold": false, "italic": false, "underline": false,
    "fontSize": 9 | 10 | 11 | 12 | 13 | 14 | 16 | 18,
    "color": "#RRGGBB",
    "fontFamily": "theme" | "arial" | "verdana" | "tahoma" | "times" | "georgia" | "courier",
    "align": "left" | "center" | "right" }
]
```
- En fazla 2 öğe, her `position` en fazla bir kez; boş/yalnız boşluk metinli öğe atlanır. `null`/yok → XSLT'ye dokunulmaz.
- Hata: 400 `{ error, field: "fixedNotes" }`.

Yanıt (`ThemeComposeResponse`), yeni alan: `"fixedNotesSupported": boolean` (tema Notlar alanı içeriyor mu — not gönderilmese de hesaplanır). Not istenip alan yoksa mevcut uyarı listesine `notes-not-found`.

## Gömme ve güvenlik (zorunlu)
- Not `XElement` ile kurulur: `<div data-xc-fixed-note="before|after" style="…">metin<br/>metin</div>`; metin **yalnız text node** (otomatik kaçış; text node'da `{`/`}` AVT değildir). XPath, `xsl:value-of`, `disable-output-escaping` kullanılmaz.
- `style` (AVT!) **yalnız sunucudaki beyaz liste eşlemesinden** üretilir (`FixedNoteStyles.ToCss()`); font ailesi sabit tablodan web-güvenli yığın + yedek. Üretilen parçada `{`/`}` varsa birleştirme kapalı başarısızlıkla durur.
- Çıktıya yorum yazılmaz (işaret `data-xc-fixed-note` niteliği). UBL namespace'leri ve `version` değişmez. Son adımda `XsltSafety.FindThreat` kalır.
- İdempotent: `xslt` yolunda `FixedNotes != null` ise önce mevcut `[data-xc-fixed-note]` öğeleri silinir.
- Sunucu doğrulaması: enum/sayı beyaz listesi, renk `^#[0-9a-fA-F]{6}\z`, mevcut `IsXmlText`, sınırlar.

## Görevler
**Backend (W1):**
- B1 `ThemeSlots/Compose/FixedNoteModels.cs`: `FixedNoteInput`, `FixedNotePosition`, `FixedNoteStyles` (boyut kümesi, font tablosu, renk regex, `ToCss()`).
- B2 `ThemeSlots/Compose/FixedNoteWriter.cs` (internal): eski notları sil → not döngüsü (`XsltNoteLoops`) → `before`: döngünün `Before` noktası; `after`: döngünün literal `td`/`th` (yoksa literal ebeveyn) atasının `ContentEnd`'i (`InsertionPointResolver` ile doğrula, geçersizse döngünün `After`'ı) → `XElement` parça → `{`/`}` kontrolü. Dönüş `(xslt, supported)`.
- B3 `ThemeAssetComposer.Compose`: blok silme → `FixedNoteWriter` → mevcut blok ekleme; `ThemeComposeInput.FixedNotes?` + `DocumentType`; `ThemeComposeResult.FixedNotesSupported`; "blok yoksa erken dön" yolu notları da kapsar.
- B4 DTO + doğrulayıcı + controller (tür: `themeId` → `theme.DocumentType`; `xslt` → kök namespace, varsayılan Invoice).
- B5 **Yalnız kritik güvenlik/yerleşim testleri** `XsltCraft.Application.Tests/ThemeSlots/FixedNoteSecurityTests.cs`: kötü metinler (`<xsl:value-of select="document('x')"/>`, `{concat(1,2)}`, `]]>`, `&amp;`, `</div><script>`) çıktıda aynen metin, `SecureXslt.Compile` derler, `<script` oluşmaz; doğrulayıcı reddi (renk enjeksiyonu, `#fff`, bilinmeyen font, boyut 99, kontrol karakteri, 3 not / aynı konum iki kez, 501 karakter); yerleşim (`before` ilk `Not:` satırından önce, `after` `Ödeme Koşulu`'ndan sonra, ikisi de `notesTableTd` içinde — `ThemeFixtures.TestTheme`); alan yoksa `supported=false` ve XSLT değişmez; iki kez uygulama tek kopya.

**Frontend (W2, B ile paralel — sözleşme yukarıda sabit):**
- F1 `services/themeComposeService.ts` (`fixedNotes` istek, `fixedNotesSupported` yanıt), `hooks/useThemeCompose.ts` (`fixedNotes` → `requestKey`, `latestRef`, önizleme **ve** `exportXslt`; `fixedNotesSupported` döndür).
- F2 `components/theme-assets/fixedNoteState.ts` (tipler, varsayılanlar, istemci sınırları, `toComposeNotes()`), `components/theme-assets/FixedNotesPanel.tsx`: iki sabit kart "Notların üstü" / "Notların altı"; her birinde araç çubuğu (B / I / U toggle `aria-pressed`, boyut `select`, renk: 6 hazır + `input type=color`, yazı tipi `select`, `AlignPicker`), `textarea` + `x/500` sayacı, textarea stil önizlemesi, temizle.
- F3 `pages/ThemeUsePage.tsx`: Banka/IBAN'ın altına `AssetAccordionSection` "Sabit notlar" (lucide `StickyNote`; durum `n not` / `Boş` / desteklenmiyorsa "Uygun değil" + açıklama), önizleme debounce (metin 1200 ms, stil/konum anında `composeNow()`); `components/theme-use/themeChecklist.ts`'e not maddesi.
- UI 2.0 tasarım dili ve kuralları: `docs/ecc/plans/ui-2-0-redesign.md` §6/§8.

**W3:** `security-reviewer` (B2–B4 farkı: AVT, kaçış, CSS, sınırlar, kapalı başarısızlık) + elle uçtan uca.

## Kabul kriterleri
- Örnek temada (`test-theme.xslt` / ekran görüntüsü) üst not "Not:" satırlarının hemen üstünde, alt not Ödeme Koşulu'nun altında, ikisi de Notlar kutusu içinde.
- Önizleme = indirilen XSLT = kaydedilen şablon.
- B/I/U, boyut, renk, font, hizalama önizlemede uygulanır; 1.0 (.NET) ve 2.0 (Saxon) temalarda render çalışır.
- Çıktıda yorum yok, namespace'ler ve `version` değişmemiş.

## Doğrulama
`dotnet test backend/XsltCraft.Application.Tests --filter "FullyQualifiedName~FixedNoteSecurity"` + mevcut `ThemeAssetComposerTests`, `ThemeComposeRequestValidatorTests` regresyonu; frontend `npm run build` + `npm run lint`; elle `/theme-use/:id` akışı (üst/alt not, stil, indir → editörde `data-xc-fixed-note` var, yorum yok → kaydet → render), bir 2.0 temada tekrar.
