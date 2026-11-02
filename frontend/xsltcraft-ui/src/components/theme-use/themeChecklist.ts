/** Tema Kullan kontrol listesi — yalnız sayfadaki mevcut durumdan türetilir. */

export type CheckTone = 'ok' | 'info' | 'warn' | 'error'

export interface CheckItem {
  id: string
  label: string
  tone: CheckTone
}

export interface ChecklistInput {
  hasLogo: boolean
  hasSignature: boolean
  bankCount: number
  /** Dolu sabit not sayısı (0–2). */
  noteCount: number
  /** Temada Notlar alanı var mı; null → bilinmiyor. */
  notesSupported: boolean | null
  customXml: boolean
  preview: { loading: boolean; error: string | null; ready: boolean; warningCount: number }
}

export function buildChecklist({ hasLogo, hasSignature, bankCount, noteCount, notesSupported, customXml, preview }: ChecklistInput): CheckItem[] {
  const items: CheckItem[] = [
    hasLogo
      ? { id: 'logo', label: 'Logo eklendi', tone: 'ok' }
      : { id: 'logo', label: 'Logo eklenmedi (isteğe bağlı)', tone: 'info' },
    hasSignature
      ? { id: 'signature', label: 'İmza / kaşe eklendi', tone: 'ok' }
      : { id: 'signature', label: 'İmza alanı boş (isteğe bağlı)', tone: 'info' },
    bankCount > 0
      ? { id: 'bank', label: `${bankCount} banka hesabı eklendi`, tone: 'ok' }
      : { id: 'bank', label: 'Banka bilgisi yok (isteğe bağlı)', tone: 'info' },
    notesSupported === false
      ? noteCount > 0
        ? { id: 'notes', label: 'Bu temada Notlar alanı yok; not eklenmedi', tone: 'warn' }
        : { id: 'notes', label: 'Bu temada sabit not alanı yok', tone: 'info' }
      : noteCount > 0
        ? { id: 'notes', label: `${noteCount} sabit not eklendi`, tone: 'ok' }
        : { id: 'notes', label: 'Sabit not yok (isteğe bağlı)', tone: 'info' },
    customXml
      ? { id: 'xml', label: "Kendi XML'inle önizleniyor", tone: 'ok' }
      : { id: 'xml', label: 'Örnek XML ile önizleniyor', tone: 'info' },
  ]

  if (preview.error) items.push({ id: 'preview', label: 'Önizleme oluşturulamadı', tone: 'error' })
  else if (preview.loading) items.push({ id: 'preview', label: 'Önizleme hazırlanıyor…', tone: 'info' })
  else if (preview.ready) items.push({ id: 'preview', label: 'Önizleme hazır', tone: 'ok' })

  if (preview.warningCount > 0)
    items.push({ id: 'warnings', label: `${preview.warningCount} yerleşim uyarısı`, tone: 'warn' })
  else if (preview.ready && !preview.error)
    items.push({ id: 'warnings', label: 'Yerleşim uyarısı yok', tone: 'ok' })

  return items
}
