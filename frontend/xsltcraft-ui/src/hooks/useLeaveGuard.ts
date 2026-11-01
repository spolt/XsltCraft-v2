import { useCallback, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { LEAVE_CONFIRM } from '../components/xslt-editor/xsltText'

/** Kaydedilmemiş metin varken üst menü gezinmesi ve çıkış sorar, sayfa kapanışı uyarır. */
export function useLeaveGuard(dirty: boolean) {
  const navigate = useNavigate()
  const confirmLeave = useCallback(() => !dirty || window.confirm(LEAVE_CONFIRM), [dirty])
  const guardedNavigate = useCallback((path: string) => {
    if (confirmLeave()) navigate(path)
  }, [confirmLeave, navigate])

  useEffect(() => {
    if (!dirty) return
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = '' }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  return { confirmLeave, guardedNavigate }
}
