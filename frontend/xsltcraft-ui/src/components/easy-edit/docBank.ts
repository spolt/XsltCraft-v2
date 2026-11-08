import type { BankInfoItem } from '../../services/previewService'
import type { EasyEditAnalyzeResponse, EasyEditDocumentBankRow } from '../../services/easyEditService'
import type { ComposeDocumentBankEdit } from '../../services/themeComposeService'
import type { BankRowLock } from '../theme-assets/BankInfoPanel'

/** Panel satırı: `docIndex` varsa belgedeki tespit edilmiş satıra karşılık gelir; yoksa yeni satırdır. */
export type DocBankItem = BankInfoItem & { docIndex?: number }

type DocBankAnalysis = Pick<EasyEditAnalyzeResponse, 'documentBank' | 'previous'>

const TR_IBAN = /^TR[0-9]{2}(?:[ \u00A0]?[0-9]){22}$/

export function isTrIban(value: string): boolean {
  return TR_IBAN.test(value.trim())
}

export interface InitialDocBankRows {
  /** Panelde düzenlenen liste: belge satırları (+ `canAdd` ise önceki blok satırları, `docIndex`'siz). */
  rows: DocBankItem[]
  /** `canAdd` değilken önceki blok satırları; kendi tablomuzda (`bankInfo`) kalır. */
  ownRows: BankInfoItem[]
}

export function initialDocBankRows(analysis: DocBankAnalysis): InitialDocBankRows {
  const bank = analysis.documentBank
  const docRows: DocBankItem[] = (bank?.rows ?? []).map((r) => ({
    bankName: r.bankName ?? '',
    iban: r.iban ?? '',
    docIndex: r.index,
  }))
  const previous: BankInfoItem[] = (analysis.previous?.bank ?? []).map((b) => ({
    bankName: b.bankName ?? '',
    iban: b.iban ?? '',
  }))
  return bank?.canAdd
    ? { rows: [...docRows, ...previous], ownRows: [] }
    : { rows: docRows, ownRows: previous }
}

export interface DocumentBankEditsResult {
  edits: ComposeDocumentBankEdit[]
  /** `current` sırasına göre satır başına IBAN hatası (yoksa `undefined`). */
  ibanErrors: (string | undefined)[]
}

const IBAN_ERROR = 'Geçerli bir TR IBAN girin (TR + 24 hane).'

/**
 * Analiz satırları ile paneldeki güncel liste arasındaki farkı kümülatif düzenlemelere çevirir.
 * Desene uymayan IBAN'lı satır gönderilmez; hatası `ibanErrors` ile döner.
 */
export function toDocumentBankEdits(
  analysisRows: EasyEditDocumentBankRow[],
  current: DocBankItem[],
): DocumentBankEditsResult {
  const edits: ComposeDocumentBankEdit[] = []
  const ibanErrors: (string | undefined)[] = current.map(() => undefined)
  const present = new Set(current.flatMap((r) => (r.docIndex == null ? [] : [r.docIndex])))

  for (const row of analysisRows) {
    if (!present.has(row.index)) edits.push({ index: row.index, bankName: '', iban: '', remove: true })
  }

  const byIndex = new Map(analysisRows.map((r) => [r.index, r]))
  current.forEach((item, i) => {
    const bankName = item.bankName.trim()
    const iban = item.iban.trim()
    if (item.docIndex != null) {
      const original = byIndex.get(item.docIndex)
      if (!original) return
      if (bankName === original.bankName.trim() && iban === original.iban.trim()) return
      if (!isTrIban(iban)) {
        ibanErrors[i] = IBAN_ERROR
        return
      }
      edits.push({ index: item.docIndex, bankName, iban, remove: false })
      return
    }
    if (bankName === '' && iban === '') return
    if (!isTrIban(iban)) {
      ibanErrors[i] = IBAN_ERROR
      return
    }
    edits.push({ index: null, bankName, iban, remove: false })
  })

  return { edits, ibanErrors }
}

/** Belge IBAN'ı bulunduğunda panelin ve compose isteğinin banka görünümü. */
export interface DocBankView {
  canAdd: boolean
  /** Panel satırı başına kilit (`current` sırasıyla). */
  locks: BankRowLock[]
  ibanErrors: (string | undefined)[]
  /** Compose isteğinin `bankInfo`'su: yalnız `!canAdd` iken önceki blok satırları; `canAdd` iken boş. */
  ownRows: BankInfoItem[]
  edits: ComposeDocumentBankEdit[]
}

/** Belge IBAN'ı bulunmadıysa null (bugünkü `bankInfo` akışı sürer). */
export function docBankView(analysis: DocBankAnalysis | null | undefined, current: DocBankItem[]): DocBankView | null {
  const bank = analysis?.documentBank
  if (!analysis || !bank?.found) return null
  const rows = bank.rows ?? []
  const byIndex = new Map(rows.map((r) => [r.index, r]))
  const ownRows = initialDocBankRows(analysis).ownRows.filter((r) => r.bankName.trim() || r.iban.trim())
  const { edits, ibanErrors } = toDocumentBankEdits(rows, current)
  return {
    canAdd: bank.canAdd,
    locks: current.map((item) => {
      const row = item.docIndex == null ? undefined : byIndex.get(item.docIndex)
      return row ? { bankName: !row.bankNameEditable, deletable: row.deletable } : {}
    }),
    ibanErrors,
    ownRows,
    // Ekleme ile dolu `bankInfo` birlikte gönderilmez (sunucu 400 döner).
    edits: ownRows.length > 0 ? edits.filter((e) => e.index != null) : edits,
  }
}
