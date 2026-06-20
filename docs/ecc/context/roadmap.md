# Roadmap (ECC bağlamı)

> Bu dosya ECC agent/eval'lerinin "yakın iş" ve "ileride" ayrımını anlaması içindir. Detaylı faz/checkbox listesi: kök `ROADMAP.md`.

## Mevcut durum
- **Faz 1–5:** tamamlandı (monorepo+auth, storage+free theme, blok editör+XSLT gen, XML binding+canlı preview, template registry+asset). PartyInfo dahil; ödeme ertelendi.
- **Faz 6 (prod readiness):** kısmi. `S3StorageService`/MinIO uçtan uca test edildi. **Açık:** prod altyapısı, KVKK için TR-bölge S3/MinIO, perf/NFR sertleştirme.
- **Faz 7 (XSLT Editor Pro):** sürüyor — XSLT editör, XPath console, profiler, snippet kütüphanesi, AI asistanı çalışıyor; dokümantasyon/entegrasyon ince işleri kaldı.
- Canvas: **V2 grid-based** editöre geçildi (serbest X/Y mm yerleşim).

## ECC adaptasyonu — bu turda yapılan
- Faz 0: bağlam zemini (`CLAUDE.md` + `docs/ecc/*`).
- Faz 1: 3 agent (`reviewer`, `security-reviewer`, `test-engineer`) + XSLT eval harness (case001) + CI non-blocking eval job.

## İleride (agent dosyası AÇMA — burada not kalsın)
- **V3 agentları**: `compiler-engineer`, `ast-engineer`, `plugin-architect`, `rag-engineer`. Gerçek AST'li compiler / plugin sistemi / RAG doğmadan agent açılmaz.
- **`compiler.md` mimari dokümanı**: şu anki "compiler" blok-ağacı→XSLT generator'dır; gerçek compiler gelince yaz.
- **ECC genişletme (Faz 2)**: `architect`/`planner`/`refactorer` agentları, kademeli workflow tanımı (new-feature / bug-fix / refactor), eval'i `prompt/` + `xpath/` ile genişletme, Playwright MCP E2E, mimari dokümanlar (`backend`/`frontend`/`database`/`ai-system`). **Faz 1 gerçekten kullanılıyorken** aç.
- **`memory/` + `sessions/` klasörleri**: açılmayacak — bağlam `CLAUDE.md` + `docs/ecc`'de yaşar.
