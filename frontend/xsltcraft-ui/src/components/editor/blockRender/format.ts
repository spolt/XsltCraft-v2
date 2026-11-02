/*
 * Üretilen XSLT'deki biçimlendirmenin tarayıcı yaklaşığı (format-number '#.##0,00' 'tr', birim kodları,
 * TRY → TL para birimi soneki). Yalnız Tasarım Alanı çizimi içindir; XSLT çıktısını etkilemez.
 */

const TR_2 = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const TR_3 = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 3 })

function toNumber(raw: string): number | null {
  if (!raw.trim()) return null
  const n = Number(raw)
  return Number.isFinite(n) ? n : null
}

export function formatAmount(raw: string): string {
  const n = toNumber(raw)
  return n === null ? '' : TR_2.format(n)
}

export function formatNumber(raw: string): string {
  const n = toNumber(raw)
  return n === null ? '' : TR_3.format(n)
}

/** 'percent' (×100) → "%10" değil, generator gibi "10%" */
export function formatPercent(raw: string, multiply: boolean): string {
  if (!multiply) return raw.trim() ? `${raw.trim()}%` : ''
  const n = toNumber(raw)
  return n === null ? '' : `${TR_3.format(n * 100)}%`
}

/** TRY/TRL → TL, diğerleri olduğu gibi. */
export function currencyLabel(code: string): string {
  const c = code.trim()
  return c === 'TRY' || c === 'TRL' ? 'TL' : c
}

/** GİB birim kodlarının sık kullanılanları (generator'daki tablonun alt kümesi). */
const UNIT_CODES: Record<string, string> = {
  C62: 'Adet', NIU: 'Adet', BX: 'Kutu', TNE: 'ton', LTR: 'lt', KGM: 'kg', GRM: 'g', MGM: 'mg',
  MTR: 'm', MMT: 'mm', KTM: 'km', MLT: 'ml', CMT: 'cm', MTK: 'm2', MTQ: 'm3', DAY: 'Gün', MON: 'Ay',
  PA: 'Paket', KWH: 'KWH', ANN: 'Yıl', HUR: 'Saat', D61: 'Dakika', D62: 'Saniye', PR: 'Çift',
  SET: 'Set', PK: 'Koli', CR: 'Kasa/Sandık', BG: 'Poşet/Torba', CEN: 'Yüz Adet', T3: '1000 adet',
}

export function unitLabel(code: string): string {
  const c = code.trim()
  return UNIT_CODES[c] ?? c
}

/** Tarih: generator ham değeri yazar; okunurluk için ISO tarihi gg-aa-yyyy gösterilir. */
export function formatDate(raw: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(raw.trim())
  return m ? `${m[3]}-${m[2]}-${m[1]}` : raw
}
