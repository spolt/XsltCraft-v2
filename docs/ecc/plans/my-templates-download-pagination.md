# Plan — Şablonlarım: seçilenleri indir · klasörü indir · sayfalama (100/sayfa)

> Durum: **onaylandı, K1–K6 kapandı, architect tamam (2026-11-21)** → uygulama (W1 backend ∥ W2 frontend) · En alttaki "Mimari kararlar" bölümü çelişkide geçerlidir · Tarih: 2026-11-21 · Kapsam: Şablonlarım (`MyXsltTemplatesPage`) + `UserXsltTemplateController` · Taslaklarım bu işe dahil değil

## İstek
1. Toplu işlem çubuğunda "Seçilenleri Sil" var ama **"Seçilenleri indir" yok** → eklenecek (öncelikli).
2. Liste bir sayfada **en fazla 100 şablon** göstersin; daha fazlası olursa **sayfalara bölünsün**.
3. **Klasördeki tüm şablonları indir** özelliği.

## Bugünkü durum (koda karşı doğrulandı)
| Konu | Şu an |
|---|---|
| Liste verisi | `GET /api/user-xslt-templates` tüm özetleri (içeriksiz) tek seferde döner; filtre/arama/sıralama istemcide (`useTemplateLibrary`) |
| Klasör sayaçları | Tüm liste üzerinden istemcide hesaplanıyor (`counts`) → sayfalama istemci tarafında yapılırsa sayaçlar bozulmaz |
| Toplu çubuk | `BulkBar` içinde: Klasöre taşı · Sabit Not Ekle · Seçilenleri Sil (`LibraryParts.tsx`) |
| Seçim | Yalnız sahip olunan şablonlar seçilebiliyor (`ownedVisible`); başlıktaki kutu "görünen tümünü" seçiyor |
| İndirme | Şablonlarım'da hiç yok; içerik yalnız `GET /{id}` ile tek tek alınabiliyor (sahip **veya** paylaşılan — `CanAccess`) |
| ZIP | Ne frontend'de (jszip/fflate yok) ne backend'de ZIP üretimi var |
| Klasör menüsü | `FolderSidebar` ⋮ menüsü: Yeniden adlandır · Renk · Sil |
| Sayfalama bileşeni | Ortak yok; `AdminUsersPage` içinde satır içi bir sayfalayıcı deseni var |

## Tasarım

### A. İndirme — sunucuda ZIP (önerilen)
Gerekçe: tek istek, yetki kontrolü tek yerde, yeni npm bağımlılığı yok; istemcide 300 şablon için 300 ayrı `GET` + JS'de sıkıştırma gerekmez.

**Yeni uç noktalar** (`UserXsltTemplateController`):
| Uç nokta | Gövde / parametre | Yetki | Çıktı |
|---|---|---|---|
| `POST /api/user-xslt-templates/download` | `{ ids: Guid[] }` (tekilleştirilir, en fazla **500**) | **Yalnız sahip** (K5); sahibi olunmayan id'ler sessizce atlanır, hiçbiri kalmazsa 404 | `application/zip` |
| `GET /api/user-xslt-templates/folders/{folderId}/download` | — | Klasör `OwnerId == userId` ve `Kind == XsltTemplate` olmalı, değilse 404; şablonlar `FolderId == folderId && OwnerId == userId` | `application/zip` |

- Sorgu yalnız `Name` + `XsltContent` projeksiyonu çeker (`AsNoTracking`).
- **Boyut tavanı:** toplam içerik ~**150 MB** karakteri aşarsa 413 + "Seçim çok büyük, daha küçük parçalar halinde indirin.".
- Yazım: .NET 10 async ZIP API'leri (`ZipArchive` + `CreateEntry(...).OpenAsync`) ile doğrudan `Response.Body`'ye akış; senkron IO'ya düşerse geçici dosya/`MemoryStream` tamponu (tavan sayesinde sınırlı). `CompressionLevel.Optimal`.
- İçerik UTF-8 (BOM'suz) yazılır; XSLT metnine dokunulmaz.
- Rate limit: mevcut politikalara bakılıp gerekiyorsa hafif bir `download` politikası (dakikada ~10).
- Üyelik kapısı **yok**: `GET /{id}` de kapısız ve 402 mesajı zaten "XSLT'yi indirebilirsiniz" diyor; Pro'dan düşen kullanıcı saklı şablonlarını indirebilmeli.

**ZIP yazıcı — Application katmanı** (`XsltCraft.Application/Templates/Download/`):
- `ITemplateArchiveWriter.WriteAsync(IEnumerable<(string Name, string Content)>, Stream, CancellationToken)` + uygulaması; DI'ye kayıt. Controller yalnız sorgu + başlıklar.
- **Dosya adı temizleme** (güvenlik açısından kritik — Zip Slip / Windows):
  - `/ \ : * ? " < > |` ve kontrol karakterleri → `_`; `..` dizileri ve baştaki/sondaki nokta-boşluk kırpılır; dizin bileşeni asla oluşmaz (tüm girdiler ZIP kökünde).
  - Windows ayrılmış adları (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`) → `_` önekli.
  - En fazla 120 karakter; boş kalırsa `sablon`.
  - `.xslt` / `.xsl` ile bitmiyorsa `.xslt` eklenir.
  - Aynı ad (büyük/küçük harf duyarsız) → `Ad (2).xslt`, `Ad (3).xslt`…
  - Türkçe karakterler korunur (.NET, ASCII dışı adlarda UTF-8 bayrağını kendisi koyar).
- ZIP adı istemcide belirlenir (CORS'ta `Content-Disposition` açığa çıkarmaya gerek kalmaz); sunucu yine RFC 5987 `filename*` başlığı yazar.

### B. Frontend — Seçilenleri indir
- `userXsltService.ts`: `downloadUserXsltZip(ids)` ve `downloadFolderZip(folderId)` → `api.post/get(..., { responseType: 'blob' })` (JWT bellekte olduğu için `<a href>` ile doğrudan indirme yapılamaz).
- Yeni `utils/saveBlob.ts`: `saveBlob(blob, fileName)` (objectURL + anchor + revoke). `useWorkspaceSaveDownload` içindeki satır içi kopya ileride buna geçirilebilir (bu işte zorunlu değil).
- `BulkBar`'a `BulkButton icon={Download}` **"Seçilenleri indir"** — "Sabit Not Ekle" ile "Seçilenleri Sil" arasına.
  - 1 şablon seçiliyse → doğrudan `.xslt` (mevcut `getUserXsltTemplate` + Blob; ZIP'e gerek yok).
  - ≥2 → ZIP: `sablonlarim-2026-11-21.zip` (aktif klasör varsa `{klasör adı}-2026-11-21.zip`).
  - Çalışırken buton devre dışı + "Hazırlanıyor…"; hata → toast ("İndirme başarısız." / 413 mesajı).
  - Seçim indirme sonrası **korunur** (silme gibi sıfırlanmaz).
- 500'den fazla seçim mümkün değil (K3, E bölümü); savunma olarak 500 üstünde uyarı verilir, istek gönderilmez.

### C. Frontend — Klasörü indir
- `FolderSidebar` ⋮ menüsüne **"Klasörü indir"** (`Download` ikonu), yeni prop `onDownload(id)`; klasör boşsa öğe devre dışı.
- Aktif görünüm bir klasörse araç çubuğunda (`StorageToolbar`, "Toplu yükle"nin yanında) **"Klasörü indir"** butonu (bkz. K2).
- Dosya adı: `{klasör adı}.zip` (aynı temizleme kuralları istemcide basit sürümle).

### D. Frontend — Sayfalama (istemci tarafı, 100/sayfa)
- Sunucu sayfalaması **gerekmez**: liste özetleri hafif, sayaçlar/arama/sıralama zaten istemcide. Sunucu sayfalaması sayaçları ve "tümünü seç"i bozar.
- `MyXsltTemplatesPage`: `PAGE_SIZE = 100`, `page` durumu; `pageItems = visible.slice((page-1)*100, page*100)`.
  - `activeKey` / `search` / `sort` değişince `page = 1`.
  - Silme/taşıma sonrası liste kısalırsa `page` son sayfaya kıstırılır.
  - Sayfa değişince liste kabı en üste kaydırılır.
- Başlık seç kutusu → yalnız **bu sayfadakileri** seçer/bırakır; etiket "Bu sayfadaki tüm şablonları seç". Seçim (`Set`) sayfalar arası **korunur**; `BulkBar` toplam seçili sayıyı gösterir.
- Yeni `components/storage/LibraryPager.tsx` (AdminUsersPage desenindeki gibi): "245 şablondan 101–200 arası" · Önceki · en fazla 5 sayfa numarası · Sonraki; `aria-current="page"`, `nav aria-label="Sayfalama"`. Yalnız `visible.length > 100` iken tablonun altında görünür.
- Araç çubuğundaki "· N şablon" metni filtrelenmiş toplamı göstermeye devam eder.
- Sayfa numarası URL'ye yazılmaz (klasör seçimi zaten `?folder=` ile `replace` ediyor; basit tutulur).

## Dokunulacak dosyalar
| Katman | Dosya | Değişiklik |
|---|---|---|
| Application | `Templates/Download/ITemplateArchiveWriter.cs`, `TemplateArchiveWriter.cs`, `ArchiveFileNames.cs` (yeni) | ZIP yazımı + ad temizleme/tekilleştirme |
| Application | DI kayıt dosyası | `ITemplateArchiveWriter` |
| Api | `Controllers/UserXsltTemplateController.cs` | 2 uç nokta, sabitler (`MaxDownloadItems`, `MaxDownloadChars`) |
| Api | Request DTO (`DownloadUserXsltRequest`) | `Ids` |
| Frontend | `services/userXsltService.ts` | 2 indirme fonksiyonu |
| Frontend | `utils/saveBlob.ts` (yeni) | Blob kaydetme |
| Frontend | `pages/MyXsltTemplatesPage.tsx` | indirme işleyicileri, sayfalama durumu, BulkBar butonu |
| Frontend | `components/storage/LibraryPager.tsx` (yeni) | sayfalayıcı |
| Frontend | `components/storage/FolderSidebar.tsx` | "Tümünü indir" menü öğesi |
| Frontend | `components/storage/StorageToolbar.tsx` (gerekirse yalnız children) | "Klasörü indir" butonu |

`MyXsltTemplatesPage.tsx` şu an 539 satır; indirme işleyicileri `components/storage/useTemplateDownload.ts` hook'una ayrılarak sayfa büyütülmez.

## Test (hız politikası: yalnız kritik)
- **Birim:** `ArchiveFileNames` — `../../etc/passwd`, `a/b\c`, `CON`, boş ad, 300 karakter, çift ad, Türkçe ad → beklenen girdi adları; üretilen ZIP'te hiçbir girdi `/`, `\` ya da `..` içermez.
- **Entegrasyon (IDOR):** başka kullanıcının şablon id'si ve başka kullanıcının klasörü → ZIP'e girmez / 404.
- UI: elle deneme (101+ şablonla sayfalama, 1 / çok seçim indirme, klasör indirme). Playwright spec'i zorunlu değil.

## Dalgalar
| Dalga | İş | Kapı |
|---|---|---|
| W1 | Backend: yazıcı + ad temizleme + 2 uç nokta + 2 kritik test | security-reviewer (IDOR, Zip Slip, DoS tavanı) |
| W2 | Frontend: `saveBlob`, servis, Seçilenleri indir, Klasörü indir, sayfalama + pager | reviewer |
| — | CHANGELOG + sürüm (öneri **2.1.5**), commit `develop`'a (push yok) | — |

Öncelik sırası istekle aynı: W2 içinde önce "Seçilenleri indir", sonra sayfalama, sonra klasör indirme.

## Sahip kararları (2026-11-21 — kapandı)
| # | Karar | Plana etkisi |
|---|---|---|
| K1 | Tek seçimde doğrudan `.xslt` | B bölümü aynen |
| K2 | Klasör indirme hem ⋮ menüsü hem aktif klasörde araç çubuğu | C bölümü aynen |
| K3 | **500'den fazla seçime izin yok → uyarı** (parçalı ZIP yok). **Bir klasöre 500'den fazla XSLT eklenemez** | Bkz. E bölümü (yeni). Seçim 500'ü aşamaz; "Seçilenleri indir" 500 üstünde uyarı. Klasör kapasitesi sunucuda zorunlu |
| K4 | "Tümü", "Favoriler" **ve "Paylaşılan"** görünümünde "tümünü indir" yok | Klasör indirme yalnız gerçek klasörlerde |
| K5 | Seçim kutusu yalnız sahip olunan şablonlarda; paylaşılanlar seçilemez | İndirme uç noktası **yalnız sahip** (`OwnerId == userId`); paylaşılan id'ler atlanır |
| K6 | Sayfa boyutu kullanıcı seçimli: **50 / 100 / 150 / 200** (varsayılan 100) | D bölümü: pager'da boyut seçici; tercih `localStorage` (try/catch, kişisel kolaylık) |

## E. Klasör kapasitesi — en fazla 500 XSLT (K3)
- Sabit: `MaxTemplatesPerFolder = 500` (yalnız `FolderKind.XsltTemplate`; Taslaklar etkilenmez).
- Sunucu zorunluluğu — XSLT'nin klasöre girdiği tüm noktalar:
  - `PATCH /{id}/folder` (`MoveToFolder`): hedef klasördeki sayım (taşınan şablon zaten oradaysa hariç) ≥ 500 → 409 `{ error: "folder_full", message: "Bir klasörde en fazla 500 şablon olabilir." }`.
  - `POST /bulk` (`BulkUpload`, `FolderId` verilmişse): mevcut + yüklenecek > 500 → tamamı reddedilir (409, kalan kapasite mesajda).
  - `Create` klasör almıyor (doğrulandı); başka atama noktası varsa architect tespit eder.
- Toplu taşıma şu an istemcide tek tek `PATCH` (6 paralel) — eşzamanlılıkta tavanın aşılmaması için architect karar verir: (a) yeni `POST /bulk-move` tek istek + tek kontrol, ya da (b) istemci ön kontrolü + sunucuda satır başı kontrol.
- İstemci ön kontrolü (kullanıcı dostu uyarı, sunucuyu tekrarlamaz): klasöre taşı / toplu yükle öncesi `counts.byFolder[id] + n > 500` → toast uyarısı, işlem başlamaz. `MoveToFolderMenu`'de dolu klasör "(dolu)" etiketli/devre dışı.
- Mevcut veride 500'ü aşmış klasör olabilir (geriye dönük): kayıt bozulmaz; yalnız yeni ekleme engellenir. Klasör indirme bu durumda 500 tavanını aşarsa 413 + uyarı (veya tavan istisnası — architect).
- Seçim tavanı: `selectedIds.size` 500'e ulaşınca yeni seçim/“bu sayfayı seç” engellenir + toast "En fazla 500 şablon seçilebilir."

---

# Mimari kararlar (architect, 2026-11-21) — yukarıdaki bölümlerle çelişirse BU bölüm geçerlidir

Doğrulanan: XSLT klasöre yalnız `MoveToFolder` + `BulkUpload` ile giriyor. Application'ın kendi DI dosyası yok; kayıt `backend/XsltCraft.Infrastructure/DependecyInjection/ServiceCollectionExtensions.cs` ("Application Services"). `IX_UserXsltTemplates_FolderId` var → migration yok.

| # | Karar |
|---|---|
| M1 | ZIP doğrudan `Response.Body`'ye akış: .NET 10 `ZipArchive.CreateAsync(..., ZipArchiveMode.Create, leaveOpen:true, null, ct)`, `entry.OpenAsync(ct)`, `await using`. MemoryStream tamponu YOK. |
| M1 → **uygulanan: M1-yedek** | Doğrudan akış denendi: `ZipArchiveEntry` akışının `DisposeAsync`ı deflate son tamponunu senkron yazıyor (Kestrel senkron IO yasağı). Bu yüzden geçici FileStream + `File(...)` kullanıldı; MemoryStream yok, yarım ZIP gönderilmez. |
| M1-yedek | `CreateAsync` derlemede yoksa: geçici `FileStream` (`FileOptions.Asynchronous`, `DeleteOnClose`) + senkron ZipArchive + `File(stream,"application/zip")`. |
| M2 | Tüm doğrulama (sahiplik, sayı, toplam uzunluk) akıştan ÖNCE; içerik `AsNoTracking().OrderBy(Name).ThenBy(Id).Select(Name, XsltContent, UpdatedAt).AsAsyncEnumerable()`. |
| M3 | Toplam uzunluk ön sorgu: `SumAsync(t => (long)t.XsltContent.Length)`. |
| M4 | İndirme uçları yeni `UserXsltTemplateDownloadController` (aynı route öneki). Mevcut controller kurucusu değişmez. |
| M5 | `XsltCraft.Application.Templates.Download`: `ITemplateArchiveWriter`, `TemplateArchiveWriter` (sealed, durumsuz), statik saf `ArchiveFileNames`, `DownloadLimits` (`MaxDownloadItems=500`, `MaxDownloadChars=150_000_000`). DI `AddSingleton`. |
| M6 | `Task WriteAsync(IAsyncEnumerable<ArchiveEntrySource> entries, Stream destination, CancellationToken ct)`; `record ArchiveEntrySource(string Name, string Content, DateTime? LastModifiedUtc)`; tekilleştirme akışta `HashSet(OrdinalIgnoreCase)`. |
| M7 | Ad temizleme: yasak dosya adı karakterleri (`/ \ : * ? " < >` ve dikey çizgi) + Unicode Cc **ve Cf** (U+202E) → `_`; `..` ve baş/son nokta-boşluk kırp; ayrılmış adlar (CON, PRN, AUX, NUL, COM0–9, LPT0–9) **ilk noktadan önceki gövdede**, kırpmadan sonra kontrol (`con.xslt` dahil) → `_` önek; 120 karakter kesimi vekil çiftini bölmez, uzantı ve `(n)` öncesi; boş → `sablon`; `.xslt`/`.xsl` değilse `.xslt`; çift ad → `Ad (2).xslt`. |
| M8 | `ContentDispositionHeaderValue("attachment")` + `SetHttpFileName` (ASCII + `filename*`), `Cache-Control: no-store`. Ad: seçim `sablonlarim.zip`, klasör `{ArchiveFileNames.SanitizeStem(folder.Name)}.zip`. |
| M9 | Rate limit kesin: `RateLimitPolicies.TemplateDownload = "template-download"`, kullanıcı başına 10/dk (`PerUserOrIp`), `Program.cs`'te `activity` yanına; iki indirme ucuna. |
| M10 | Yeni `POST /bulk-move`: tek istek, tek kapasite kontrolü, tek `SaveChanges`. |
| M11 | bulk-move stub güncelleme: önce `Where(ids.Contains(Id) && OwnerId==userId).Select(Id, FolderId)`; stub `Attach` + `Property(FolderId).CurrentValue = folderId` + **`IsModified = true` açıkça** (null hedef tuzağı). İçerik yüklenmez. |
| M12 | `CheckFolderCapacityAsync(folderId, incoming)` → 409 ya da null; sayım `CountAsync(t => t.FolderId == folderId)`. MoveToFolder: zaten oradaysa incoming=0 (204). BulkUpload: doğrulama döngüsünden sonra, `Created.Count` ile, SaveChanges öncesi. Klasörsüz hedefte sınır yok. `Templates/TemplateLimits.MaxTemplatesPerFolder = 500`. |
| M13 | Mevcut >500 klasörler bozulmaz; yeni ekleme 409. Klasör indirmede sayı tavanı yok, yalnız 150 M karakter (413). |
| M14 | Yalnız sahip: `OwnerId == userId`; klasör `OwnerId==userId && Kind==XsltTemplate` değilse 404 (403 değil). |
| M15 | ZIP başına 1 etkinlik: `RecordAsync(userId, Download, null, "Xslt")` akış sonrası; tekli indirmede istemci `reportDownload('Xslt', id)`. |
| M16 | DTO'lar `XsltCraft.Application/DTO/`. |
| M17 | Frontend hata ayrıştırma `parseGateError` (`services/entitlementService.ts`; Blob gövde). |
| M18 | Pager çubuğu `visible.length > 50` iken görünür (boyut seçici için); sayfa düğmeleri `totalPages > 1` iken. |
| M19 | Başlık kutusu yalnız bu sayfanın sahip olunanlarını birleştirir/çıkarır; tavan aşılacaksa sayfa **hiç eklenmez** + uyarı "Bu sayfadakiler eklenince seçim 500'ü aşar. En fazla 500 şablon seçilebilir."; tek satırda 500'de durur + uyarı. |
| M20 | `crypto.randomUUID` yok; tarih damgası yerel `getFullYear/Month/Date` + `padStart` (UTC değil). |

## API sözleşmesi (donduruldu)
Tümü `[Authorize]`. Hata gövdeleri JSON.

**A. `POST /api/user-xslt-templates/download`** (rate `template-download`) — istek `{ "ids": [...] }` (tekilleştirilir)
- 200 `application/zip`, `Content-Disposition` (`sablonlarim.zip`), `Cache-Control: no-store`
- 400 `{ message: "Şablon seçilmedi." }` / `{ message: "Tek seferde en fazla 500 şablon indirilebilir." }`
- 404 `{ message: "İndirilecek şablon bulunamadı." }`
- 413 `{ error: "download_too_large", message: "Seçim çok büyük, daha küçük parçalar halinde indirin." }`
- 429 boş gövde + `Retry-After`

**B. `GET /api/user-xslt-templates/folders/{folderId:guid}/download`** (rate `template-download`)
- 200 zip `{temiz klasör adı}.zip`
- 404 `{ message: "Klasör bulunamadı." }` (yok/başkasının/Draft)
- 404 `{ error: "folder_empty", message: "Klasörde indirilecek şablon yok." }`
- 413 `{ error: "download_too_large", message: "Klasör tek seferde indirilemeyecek kadar büyük; şablonları seçerek parçalar halinde indirin." }`
- 429 boş gövde + `Retry-After`

**C. `POST /api/user-xslt-templates/bulk-move`** (üyelik kapısı yok) — istek `{ "ids": [...], "folderId": "guid" | null }`
- 200 `{ "movedIds": [...] }` (sahip olunan ve artık hedefte olanlar, zaten orada olanlar dahil; yabancılar sessizce düşer)
- 400 `{ message: "Şablon seçilmedi." }` / `{ message: "Tek istekte en fazla 500 şablon taşınabilir." }` / `{ message: "Geçersiz klasör." }`
- 409 `{ error: "folder_full", message: "Bir klasörde en fazla 500 şablon olabilir. Bu klasöre en fazla {remaining} şablon daha eklenebilir.", capacity: 500, remaining: n }` — hiçbir şey taşınmaz

**D. `PATCH /{id}/folder`** (mevcut): + aynı 409 `folder_full`.
**E. `POST /bulk`** (mevcut): + aynı 409 `folder_full`; o istekteki hiçbir öğe kaydedilmez (önceki partiler kalır).

## Görev dağıtımı
| Görev | Alan | Agent | Kapsam |
|---|---|---|---|
| W1-A | backend.md | general-purpose (backend) | Arşiv yazıcısı + `UserXsltTemplateDownloadController` + rate limit + DI (M1–M9, M14–M16) |
| W1-B | backend.md + database.md | aynı backend agent (W1-A sonrası; derleme kilidi çakışmasın) | `TemplateLimits`, `CheckFolderCapacityAsync`, `MoveToFolder`/`BulkUpload` 409, `bulk-move` (M10–M12) |
| W1-T | — | test-engineer | T1 `Application.Tests/Templates/ArchiveFileNamesTests.cs` (+ senkron IO'da fırlatan akış testi); T2 `XsltWorker.Tests/Api/UserXsltTemplateDownloadTests.cs` (IDOR, paylaşılan girmez, yabancı/Draft klasör 404); T3 `UserXsltTemplateFolderCapacityTests.cs` (PATCH 409/204, bulk 409 hiç kayıt yok, bulk-move 409, yabancı id atlanır, null hedef kalıcı). |
| W2 (F1→F2→F3) | frontend.md | general-purpose (frontend), W1 ile paralel | F1 indirme (`saveBlob`, servis, `useTemplateDownload`, BulkBar butonu, ⋮ "Tümünü indir", araç çubuğu "Klasörü indir"); F2 sayfalama (`usePagedList`, `useCappedSelection`, `LibraryPager`, 50/100/150/200, localStorage `xsltcraft-library-page-size`); F3 kapasite UI (`bulkMoveUserXslt`, ön kontrol, `MoveToFolderMenu` "(dolu)", `BulkUploadModal` `folderCounts`). Sayfa dosyası ≤ ~560 satır. |
| Kapı 1 | — | security-reviewer | IDOR, Zip Slip/adlar, DoS (rate + 150 M + 500), senkron IO yokluğu, bulk-move sahiplik |
| Kapı 2 | — | reviewer | Tüm diff (yüksek/kritik bulgular) |
| Son | — | ana oturum | CHANGELOG + 2.1.5, `develop`'a commit (push yok), `backend.md`/`frontend.md` kısa not |
