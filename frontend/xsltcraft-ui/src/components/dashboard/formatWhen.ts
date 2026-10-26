const TIME = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' })
const DAY_MONTH = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long' })
const FULL = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })

function startOfDay(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
}

/** "Bugün 14:32" · "Dün" · "22 Ekim" · (başka yıl) "22 Ekim 2025". Geçersiz tarihte boş. */
export function formatWhen(iso: string, now: Date = new Date()): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const diffDays = Math.round((startOfDay(now) - startOfDay(d)) / 86_400_000)
  if (diffDays === 0) return `Bugün ${TIME.format(d)}`
  if (diffDays === 1) return 'Dün'
  return d.getFullYear() === now.getFullYear() ? DAY_MONTH.format(d) : FULL.format(d)
}
