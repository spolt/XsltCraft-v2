import { useCallback, useEffect, useSyncExternalStore } from 'react'
import { getMyTemplates } from '../services/templateService'
import { getUserXsltTemplates } from '../services/userXsltService'
import { useAuthStore } from '../store/authStore'
import { SOURCE_LABEL, draftOpenPath, draftSource, xsltOpenPath, type WorkSource } from '../utils/workRoutes'

/** Kullanıcının bir çalışması: Tasarım Alanı taslağı ya da XSLT şablonu. */
export interface WorkItem {
  /** Liste anahtarı: `${kind}:${id}` */
  key: string
  id: string
  kind: 'draft' | 'xslt'
  name: string
  /** ISO tarih */
  updatedAt: string
  /** Taslaklarda belge türü; XSLT'de bilinmiyor (null). */
  documentType: 'Invoice' | 'Despatch' | null
  source: WorkSource
  /** "Tasarım alanı" | "Temadan" | "XSLT" */
  sourceLabel: string
  /** "Aç" rotası (DraftsPage / MyXsltTemplatesPage ile aynı). */
  to: string
  /** Başkası tarafından paylaşılan XSLT. */
  isShared: boolean
}

export interface RecentWorkState {
  /** updatedAt'e göre yeniden eskiye, taslak + XSLT birleşik. */
  items: WorkItem[]
  loading: boolean
  /** İki kaynak da yüklenemediyse true (biri yüklendiyse kısmi liste döner). */
  error: boolean
}

const TTL_MS = 60_000

let snapshot: RecentWorkState & { loadedAt: number } = { items: [], loading: false, error: false, loadedAt: 0 }
let inflight: Promise<void> | null = null
let generation = 0
const listeners = new Set<() => void>()

function emit(next: typeof snapshot) {
  snapshot = next
  listeners.forEach((l) => l())
}

function subscribe(l: () => void) {
  listeners.add(l)
  return () => {
    listeners.delete(l)
  }
}

/** Önbelleği (60 sn) kullanarak yükler; force=true önbelleği atlar. Aynı anda tek istek. */
export function loadRecentWork(force = false): Promise<void> {
  if (inflight) return inflight
  if (!force && snapshot.loadedAt && Date.now() - snapshot.loadedAt < TTL_MS) return Promise.resolve()
  const gen = generation
  emit({ ...snapshot, loading: true })
  inflight = Promise.allSettled([getUserXsltTemplates(), getMyTemplates()])
    .then(([xsltRes, draftRes]) => {
      if (gen !== generation) return // bu arada oturum değişti
      const items: WorkItem[] = []
      if (xsltRes.status === 'fulfilled') {
        for (const x of xsltRes.value) {
          items.push({
            key: `xslt:${x.id}`, id: x.id, kind: 'xslt', name: x.name, updatedAt: x.updatedAt,
            documentType: null, source: 'xslt', sourceLabel: SOURCE_LABEL.xslt, to: xsltOpenPath(x.id),
            isShared: !x.isOwner,
          })
        }
      }
      if (draftRes.status === 'fulfilled') {
        for (const t of draftRes.value) {
          const source = draftSource(t)
          items.push({
            key: `draft:${t.id}`, id: t.id, kind: 'draft', name: t.name, updatedAt: t.updatedAt,
            documentType: t.documentType, source, sourceLabel: SOURCE_LABEL[source], to: draftOpenPath(t.id),
            isShared: false,
          })
        }
      }
      items.sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt))
      const failed = xsltRes.status === 'rejected' && draftRes.status === 'rejected'
      emit({ items, loading: false, error: failed, loadedAt: failed ? 0 : Date.now() })
    })
    .finally(() => {
      inflight = null
    })
  return inflight
}

/** Bir kayıt/silme sonrası bir sonraki kullanımda tazelensin. */
export function invalidateRecentWork(): void {
  snapshot = { ...snapshot, loadedAt: 0 }
}

// Oturum (kullanıcı) değişince önbelleği sıfırla.
useAuthStore.subscribe((s, prev) => {
  if (s.user?.id === prev.user?.id) return
  generation++
  inflight = null
  emit({ items: [], loading: false, error: false, loadedAt: 0 })
})

const getSnapshot = () => snapshot

/**
 * Son çalışmalar (taslak + XSLT). İlk kullanımda yükler; modül seviyesinde 60 sn önbellek,
 * tüm bileşenler aynı veriyi paylaşır. `reload()` önbelleği atlayıp yeniden çeker.
 * `{ lazy: true }` → otomatik yüklemez; çağıran uygun anda `loadRecentWork()` çağırır (ör. arama odağında).
 */
export function useRecentWork(options?: { lazy?: boolean }): RecentWorkState & { reload: () => Promise<void> } {
  const lazy = options?.lazy ?? false
  const snap = useSyncExternalStore(subscribe, getSnapshot)
  useEffect(() => {
    if (!lazy) void loadRecentWork()
  }, [lazy])
  const reload = useCallback(() => loadRecentWork(true), [])
  return { items: snap.items, loading: snap.loading, error: snap.error, reload }
}
