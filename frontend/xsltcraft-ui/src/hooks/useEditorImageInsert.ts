import { useCallback, useRef, type ChangeEvent, type RefObject } from 'react'

/**
 * Monaco "Resim Ekle" bağlam menüsü: görsel seçtirir ve base64 gömülü `<img>` ekler.
 * Monaco ilk mount'taki geri çağrıyı tutar; yalnız ref okuduğu için bayatlamaz.
 */
export function useEditorImageInsert(insertTextAtLineRef: RefObject<((lineNumber: number, text: string) => void) | null>) {
  const inputRef = useRef<HTMLInputElement>(null)
  const pendingLineRef = useRef<number | null>(null)

  const request = useCallback((lineNumber: number) => {
    pendingLineRef.current = lineNumber
    inputRef.current?.click()
  }, [])

  const handleFile = useCallback((e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    const line = pendingLineRef.current
    if (!file || line === null) return
    pendingLineRef.current = null
    const reader = new FileReader()
    reader.onload = (ev) => {
      const dataUrl = ev.target?.result as string
      insertTextAtLineRef.current?.(line, `<div><img src="${dataUrl}" style="max-width:100%;height:auto;" /></div>`)
    }
    reader.readAsDataURL(file)
  }, [insertTextAtLineRef])

  return { inputRef, request, handleFile }
}
