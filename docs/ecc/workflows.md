# Workflows — agent/skill/eval orkestrasyonu

> Amaç: Faz 1-2'de kurulan agent (`.claude/agents/`), skill (`.claude/skills/`) ve eval'leri (`Category=Eval`) ortak görevlerde **kademeli** kullanmak.
> **İlke: over-routing'den kaçın.** Her görev tüm zinciri gerektirmez. Solo akışta agent'ı çağırmak yerine ilgili skill'i in-context kullanmak çoğu zaman yeter; ağır zinciri yalnız risk/karmaşıklık gerçekten yüksekken aç.

## Ne zaman ne?
| Sinyal | Aç |
|--------|-----|
| Belirsiz tasarım / katman kararı | `architect` |
| Net iş, sadece sıraya koymak | `planner` (veya doğrudan başla) |
| XML/XSLT/upload/auth/AI'a dokunan diff | `security-reviewer` + `xss-xxe-checklist` skill |
| Herhangi bir anlamlı diff | `review-checklist` skill (solo) → büyük/riskliyse `reviewer` agent |
| Davranış korunmalı, yapı iyileşecek | `refactorer` |
| Yeni davranış / regresyon | `test-engineer` + eval (`Category=Eval`) |

## 1. new-feature
`Planner → Architect → Impl → Reviewer → Security → Tests → (Playwright) → Merge`
- **Planner/Architect:** kapsam belirsizse ikisi; net ve küçükse atla, doğrudan impl.
- **Impl:** mevcut util/servisi yeniden kullan (`docs/ecc/architecture/*`). Katman kuralı: `Application ↛ Infrastructure`.
- **Security:** özellik XML/XSLT/upload/auth/AI yüzeyine dokunuyorsa **zorunlu** (`security-reviewer`). Değilse atlanabilir.
- **Tests:** generator → `XsltCraft.Tests`; AI → `Application.Tests` golden; kritik invariant → eval case (`docs/ecc/evaluations/`).
- **Playwright:** UI akışı değiştiyse (Faz 2 E2E hazır olduğunda).
- **Gate:** `dotnet test` + frontend lint/build yeşil; CI eval non-blocking uyarı temiz.

## 2. bug-fix
`Reproduce → RCA → Fix → Regression Test → (gerekirse Review) → Merge`
- **Reproduce:** önce hatayı gösteren **başarısız test** yaz (eval/unit). Düzeltmeden önce kırmızı olmalı.
- **RCA:** kök neden; semptomu değil nedeni düzelt.
- **Fix → test yeşile döner.** `architect`/`security` çoğu bug-fix'te **opsiyonel** — yalnız güvenlik/mimari kök nedende aç.
- **Review:** dokunulan alan hassassa (`security-reviewer`/`reviewer`), değilse `review-checklist` skill yeter.

## 3. refactor
`Architect (kapsam) → Refactorer → Reviewer → Tests → Merge`
- **Refactorer:** davranış değişmez; öncesi/sonrası `dotnet test` + golden/eval snapshot **değişmemeli**.
- Snapshot değiştiyse bu davranış değişimidir → dur, bilinçli değilse geri al.
- Public kontrat / üretilen XSLT / namespace değişecekse `architect` + kullanıcı onayı.

## Genel kurallar
- Her merge öncesi: pre-commit hook geçer (`--no-verify` yok); `dotnet format` + lint temiz.
- Branch: `feature/faz-N-konu | fix/konu | refactor/...` (hook konvansiyonu).
- Eval CI'da non-blocking (uyarı) — olgunlaşınca blocking'e geçilir.
- Şüphede solo kal: agent zinciri yerine skill + doğrudan iş; gerçekten gerekince ölçekle.
