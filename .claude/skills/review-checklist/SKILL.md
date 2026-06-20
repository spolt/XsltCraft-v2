---
name: review-checklist
description: XsltCraft kod inceleme checklist'i — bug, SOLID/mimari ihlali, okunabilirlik, edge case, performans ve duplicate logic. Bir diff/PR/yeni kodu incelerken kullan. reviewer agent'ı bu checklist'i çağırır; solo akışta agent'ı atlayıp skill'i in-context kullanmak çoğu zaman yeter.
---

# Code Review Checklist (XsltCraft)

> Bağlam: `docs/ecc/standards.md` (mimari kurallar) + `docs/ecc/context/constraints.md` (UBL-TR/güvenlik kısıtları).
> **Not:** `reviewer` agent'ı bu listeyi uygular. Tek başına çalışırken agent'ı çağırmadan bu skill'i in-context açıp diff'i bu maddelere göre geç — çoğu durumda yeterli; çakıştırma (agent + skill aynı anda gereksiz).

## Önce kapsamı belirle
- İncelenecek değişikliği al: `git diff` / `git diff --staged`. Sadece değişen kodu ve onun dokunduğu yüzeyi incele, tüm repoyu değil.
- Güvenlik-hassas dosya mı? (XML/XSLT, upload, auth, AI, storage) → `xss-xxe-checklist` skill'i / `security-reviewer` ile birlikte değerlendir.

## 1. Doğruluk & bug
- [ ] Null / boundary / off-by-one; boş koleksiyon, eksik sözlük anahtarı.
- [ ] `async/await` doğru mu? Yutulmuş `Task`, eksik `await`, `async void` yok.
- [ ] `CancellationToken` akışı kesilmemiş (özellikle streaming/IO).
- [ ] Exception: yutulmuş `catch` yok; prod'da generic mesaj + `LogError`, dev'de `ex.Message`.
- [ ] XPath/namespace varsayımları doğru (UBL-TR `n1/cbc/cac/ext`).

## 2. SOLID & mimari (XsltCraft kuralları)
- [ ] **`Application` katmanı `Infrastructure`'a bağımlı DEĞİL** (en sık ihlal — import'ları kontrol et).
- [ ] Somut tip `new`'lenmemiş; interface üzerinden tüketiliyor (`IStorageService`, `IAiAssistantProvider`, `IXsltGeneratorService`).
- [ ] Yeni `static` service / global state eklenmemiş.
- [ ] Tek sorumluluk; büyüyen switch/handler ayrı metoda bölünmüş; dosya küçük kalmış.
- [ ] DI kaydı tek yerde (`ServiceCollectionExtensions`).

## 3. Okunabilirlik
- [ ] İsimlendirme niyeti anlatıyor; ölü kod / yorumlanmış blok yok.
- [ ] Magic string/number yerine sabit/enum.
- [ ] Çevre koduyla aynı stil (yorum yoğunluğu, idiom).

## 4. Edge case
- [ ] Boş blok-tree, eksik/`null` binding, çok büyük girdi, eşzamanlılık (template kilidi).
- [ ] Üretilen XSLT her senaryoda **derlenebilir** mi? (`XslCompiledTransform.Load` geçer.)
- [ ] Sınır boyutları: request size limit (`PreviewRaw` ~1MB, `ValidateXslt` ~512KB).

## 5. Performans
- [ ] Gereksiz allocation / kopya; sıcak yolda string birleştirme.
- [ ] N+1 sorgu / döngü içinde IO.
- [ ] NDJSON streaming `PipeWriter` + `FlushAsync` ile; buffer/`MemoryStream` ile boğulmuyor.

## 6. Duplicate logic
- [ ] Yeni yardımcı yazmadan önce `Grep` ile mevcut util/servis ara (örn. `XmlEscape`, `XsltSafety`, `PatternSelector`).
- [ ] Kopyala-yapıştır mantık tek yere toplanmış.

## Çıktı formatı
Bulgular önem sırasına göre: **[Blocker] / [Önemli] / [Küçük] / [Övgü]**. Her biri: `dosya:satır` + sorun + somut öneri (kısa kod olabilir). Spekülatif olma; emin değilsen "doğrula" diye işaretle. Sonunda 1-2 cümle özet.
