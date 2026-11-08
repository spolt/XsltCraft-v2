/**
 * Mod geçişinde (XSLT Editör ↔ Kolay düzenle) aynı şablonun düzenleme kilidi bırakılmaz: geçişi başlatan taraf
 * `markLockHandoff(id)` çağırır, eski sayfanın kilidi çıkışta bu işareti görünce bırakmaz, yeni sayfa kilidi
 * aldıktan sonra işaret temizlenir. Yeni sayfa kilidi hiç almazsa işaret kendiliğinden düşer (kilit sunucuda süresi
 * dolunca serbest kalır).
 */
const HANDOFF_TTL_MS = 15_000

const pending = new Map<string, number>()

export function markLockHandoff(templateId: string): void {
  pending.set(templateId, Date.now() + HANDOFF_TTL_MS)
}

export function isLockHandoff(templateId: string): boolean {
  const until = pending.get(templateId)
  if (until === undefined) return false
  if (until < Date.now()) {
    pending.delete(templateId)
    return false
  }
  return true
}

export function clearLockHandoff(templateId: string): void {
  pending.delete(templateId)
}
