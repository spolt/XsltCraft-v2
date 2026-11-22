/** Blob'u tarayıcıda dosya olarak kaydettirir (objectURL + geçici bağlantı). */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  // Büyük ZIP'lerde bazı tarayıcılar indirmeyi tıklamadan sonra başlatır; URL'yi hemen bırakma.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

const MAX_STEM = 120

/**
 * Dosya adı gövdesi (uzantısız): `toDownloadName` ile aynı kural — harf/rakam (Türkçe dahil) dışı → `_`.
 * En fazla 120 karakter; anlamlı karakter kalmazsa `sablon`.
 */
export function fileStem(name: string): string {
  const stem = name.trim().replace(/[^a-z0-9çğıöşüÇĞİÖŞÜ-]/gi, '_').slice(0, MAX_STEM)
  return /[^_-]/.test(stem) ? stem : 'sablon'
}

/** Yerel saatle `YYYY-MM-DD` (UTC değil — gece yarısı civarında yanlış güne düşmesin). */
export function localDateStamp(d: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
