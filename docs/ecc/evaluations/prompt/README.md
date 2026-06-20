# Prompt eval pack — AI pattern pipeline

> Roadmap'teki "prompt/ (AI pattern pack'leri)" eval'i. **Yeni Category=Eval testi eklenmedi** çünkü AI pipeline zaten **blocking** unit/golden testlerle kilitli; bunları `Category=Eval`'e taşımak (non-blocking) korumayı *zayıflatır*. Bu doküman o testleri prompt regresyon paketi olarak işaretler.

## Mevcut koruma (XsltCraft.Application.Tests/Ai)
| Test | Neyi kilitler |
|------|---------------|
| `PatternSelectorTests` | UBL-TR tetikleyici → doğru pattern pack seçimi (invoice-note, supplier/customer-party-address, invoice-line, legal-monetary-total, …); XSLT sinyali; **≤4 pattern** sınırı |
| `PromptRegistryTests` | Embedded `Prompts/Core` + `Prompts/Patterns/*.md` yükleme, YAML frontmatter parse |
| `BuildMessagesGoldenTests` | `PromptTemplates.BuildMessages` çıktısı (first turn / multi-turn history / refactor) — Verify golden snapshot |
| `IntentClassifierTests` | Code vs Smalltalk niyet ayrımı |
| `XsltSummarizerTests` | XSLT yapı özeti (regex, mid-edit güvenli) |

## Kural: yeni pattern pack eklerken
1. `Prompts/Patterns/<Yeni>.md` ekle.
2. `PatternSelectorTests.Cases`'e tetikleyici → `<yeni-id>` case'i ekle.
3. Davranış değiştiyse `BuildMessagesGoldenTests` golden snapshot'ını **bilinçli** güncelle (`.received` → `.verified`).

Bu testler ana suite'te (blocking) koşar; ayrıca bir non-blocking kopya tutulmaz (duplicate logic yok).
