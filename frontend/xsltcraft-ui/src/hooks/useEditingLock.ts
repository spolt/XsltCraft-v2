import { useCallback, useEffect, useRef, useState } from 'react'
import { acquireEditingLock, releaseEditingLock } from '../services/userXsltService'
import { clearLockHandoff, isLockHandoff } from './lockHandoff'

const LOCK_HEARTBEAT_MS = 30_000

export interface UseEditingLockOptions {
  /** Kilit başka kullanıcıya geçtiği anda (yükselen kenar) bir kez çağrılır. */
  onLost?: (ownerName: string | null) => void
  /**
   * Çıkışta (şablon değişimi / unmount) kilidin bırakılıp bırakılmayacağı. Varsayılan: her zaman. Mod geçişi
   * (`markLockHandoff`) sürüyorsa kilit her durumda bırakılmaz.
   * Örn. oturum kapanmışken isteği atlamak için `() => !!useAuthStore.getState().accessToken`.
   */
  shouldRelease?: () => boolean
}

export interface EditingLockState {
  lockedByOther: boolean
  lockOwnerName: string | null
  /** Sunucu kilit çakışması bildirdiğinde (ör. kayıtta 423) salt-okunura geçer; uyarı tetiklenmez. */
  markLockedByOther: () => void
}

/**
 * Kayıtlı şablon için eşzamanlı düzenleme kilidi: açılışta kilit alır, heartbeat ile tazeler,
 * çıkışta bırakır. `templateId` boşken hiçbir istek atılmaz.
 */
export function useEditingLock(
  templateId: string | null | undefined,
  options: UseEditingLockOptions = {},
): EditingLockState {
  const [lockedByOther, setLockedByOther] = useState(false)
  const [lockOwnerName, setLockOwnerName] = useState<string | null>(null)
  const wasLockedRef = useRef(false)

  // Geri çağrılar effect'i yeniden başlatmasın: son sürüm ref'te tutulur.
  const { onLost, shouldRelease } = options
  const onLostRef = useRef(onLost)
  const shouldReleaseRef = useRef(shouldRelease)
  useEffect(() => {
    onLostRef.current = onLost
    shouldReleaseRef.current = shouldRelease
  }, [onLost, shouldRelease])

  useEffect(() => {
    if (!templateId) return
    const id = templateId
    let cancelled = false

    async function tick() {
      try {
        const lock = await acquireEditingLock(id)
        if (cancelled) return
        // Mod geçişiyle devralınan kilit artık bu sayfada.
        clearLockHandoff(id)
        setLockedByOther(lock.lockedByOther)
        setLockOwnerName(lock.editingUserName)
        if (lock.lockedByOther && !wasLockedRef.current) onLostRef.current?.(lock.editingUserName)
        wasLockedRef.current = lock.lockedByOther
      } catch {
        // sessizce yoksay (yetki/ağ hatası)
      }
    }

    tick()
    const intervalId = setInterval(tick, LOCK_HEARTBEAT_MS)
    return () => {
      cancelled = true
      clearInterval(intervalId)
      if (!isLockHandoff(id) && (shouldReleaseRef.current?.() ?? true)) releaseEditingLock(id).catch(() => {})
      wasLockedRef.current = false
    }
  }, [templateId])

  const markLockedByOther = useCallback(() => {
    setLockedByOther(true)
    wasLockedRef.current = true
  }, [])

  return { lockedByOther, lockOwnerName, markLockedByOther }
}
