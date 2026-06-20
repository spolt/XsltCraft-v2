# XsltCraft — ECC Adaptasyon Yol Haritası

> **Amaç:** ECC'nin (affaan-m, ~60 agent / 232 skill) tamamını kurmak DEĞİL; XsltCraft'a özel, bakımı kolay, gerçekten kullanılan hafif bir alt küme oluşturmak.
> **Bu doküman:** Claude Code (VSCode) için planlama girdisidir. Faz faz uygula; her fazı bir önceki gerçekten KULLANILIYORken aç.

---

## 0. Önce verilecek karar: klasör konvansiyonu

ECC'yi custom `.ecc/` ağacı olarak kurarsan Claude Code agent/skill'leri **otomatik keşfetmez**. İki seçenekten birini seç:

- **A) Native (önerilen):** Agent'ları `.claude/agents/*.md`, skill'leri `.claude/skills/`'e koy. Otomatik tetiklenir. Mimari/ADR/eval gibi insan-okur dokümanlar `docs/ecc/` altında dursun.
- **B) ECC plugin + prune:** `/plugin marketplace add` ile ECC'yi kur, sonra XsltCraft'a uymayan agent/skill'leri sil.

> Karar: __________ (A önerilir; B sadece ECC'nin hazır skill'lerini ciddi kullanacaksan)

---

## Faz 0 — Temel (1 oturum, en yüksek kaldıraç)

Agent yazmadan önce bağlamı kur; bu, sonraki her agent'ı akıllı yapar.

**Görevler:**
1. `docs/ecc/context/` altında 4 kısa dosya (her biri 1 sayfa max):
   - `project-overview.md` — README'den özet, ürün hedefi, kullanıcı
   - `tech-stack.md` — .NET 10 / React 19 / Postgres / MinIO / Saxon HE / Ollama+Gemini
   - `constraints.md` — UBL-TR 2.1, XSLT 2.0, GİB kuralları (150KB limit, UTF-8, QR), namespace bozma yasağı
   - `roadmap.md` — yakın iş + "ileride" notları (V3 fikirleri buraya, agent olarak değil)
2. `standards.md` — Application katmanı Infrastructure'a asla bağımlı olmaz; interface tercih et; static service'ten kaçın; feature-based klasör; dosyaları küçük tut.
3. `docs/ecc/decisions/` — ADR 001–006 (clean-arch, zustand, saxon-he, monaco, react19, minio). **Her biri tek paragraf:** Context / Alternatives / Decision / Reasoning / Consequences. Retrospektif, kısa tut.

**Çıktı:** Agent'ların okuyacağı tek, güncel "doğru kaynak" seti. memory/ + sessions/ klasörlerini AÇMA — bağlam burada + CLAUDE.md'de yaşasın.

---

## Faz 1 — Rant ödeyen 3 agent + en kritik eval

Hemen değer üreten minimum set. Önce bunları gerçekten kullan.

**Agentlar (`.claude/agents/`):**
- `reviewer.md` — bug, SOLID ihlali, okunabilirlik, edge case, perf, duplicate logic
- `security-reviewer.md` — **XsltCraft için kritik:** XXE, XPath injection, prompt injection (AI asistanı), SSRF, path traversal, file-upload, JWT/refresh-token akışı
- `test-engineer.md` — xUnit + Verify golden snapshot + integration testleri (mevcut altyapının üstüne)

**Eval harness (en yüksek riskli alandan başla):**
- `docs/ecc/evaluations/xslt/` ve `.../preview/` altında case'ler
- `case001`: request + requirements (namespace/format/template KORUNMALI) + "değişken silme / loop kırma yasak". Verify.Xunit'e bağla.

**Skills (`.claude/skills/`):**
- `review-checklist/SKILL.md` — bug / SOLID / edge case / perf checklist. **Not:** `reviewer` agent'ı bunu çağırır; çakıştırma. Solo akışta agent'ı atlayıp skill'i in-context kullanmak çoğu zaman yeter.
- `xss-xxe-checklist/SKILL.md` — XXE / XPath injection / SSRF / path traversal; `security-reviewer` agent'ı bunu kullanır.

**MCP:** Serena (kod gezme), Context7 (React/Saxon/.NET/Tailwind dokümanı), GitHub MCP (PR/issue/release).

**Bitiş kriteri:** En az 1 gerçek PR'ı reviewer + security-reviewer'dan geçirdin, eval harness 1 regresyonu yakaladı.

---

## Faz 2 — Genişleme (Faz 1 kullanılıyorsa)

- **Agentlar:** `architect.md`, `planner.md`, `refactorer.md`
- **Workflow'ları kademeli tanımla** (over-routing'i önle):
  - `new-feature`: Planner → Architect → Impl → Reviewer → Security → Tests → Playwright → Merge
  - `bug-fix`: Reproduce → RCA → Fix → Regression Test → (gerekirse) Review → Merge  *(architect/security opsiyonel)*
  - `refactor`: Architect → Refactorer → Reviewer → Tests → Merge
- **Eval'i genişlet:** `prompt/` (AI pattern pack'leri) + `xpath/`
- **Playwright MCP** E2E: Upload XML, Upload XSLT, Edit blocks, Generate preview, Validate output
- **Skill:** `playwright-e2e/SKILL.md` — yukarıdaki E2E senaryolarını yazma/koşturma adımları (Playwright MCP ile eşleşir)
- **Mimari doküman:** `backend.md`, `frontend.md`, `database.md`, `ai-system.md`

---

## Ertelenecekler (şimdi YAPMA)

- **V3 agentları** (compiler-engineer, ast-engineer, plugin-architect, rag-engineer) → `roadmap.md`'de not. Gerçek AST'li compiler / plugin sistemi doğmadan agent dosyası açma.
- **`compiler.md` mimari dokümanı** → şu anki "compiler" blok-ağacı→XSLT generator'ı; gerçek compiler gelince yaz.
- **memory/ + sessions/ klasörleri** → context/ + CLAUDE.md yeter; çoğaltma.

---

## Açık sorular (Claude Code ile netleştir)

1. Klasör konvansiyonu A mı B mi? (yukarıdaki karar)
2. Eval'ler CI'da (`ci.yml`) blocking mi çalışacak, yoksa lokal mi?
3. security-reviewer'ın XXE/SSRF kontrolleri sadece review pass'inde mi, yoksa eval harness'a da gömülecek mi? (öneri: ikisi de)
4. Antigravity bu agent setini kullanacak mı, yoksa sadece senin lokal harness'ın mı? (kullanacaksa konvansiyonun repo'da paylaşılır olması şart)
