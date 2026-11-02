import type { RefObject } from 'react'
import type { editor as MonacoEditor } from 'monaco-editor'

const isHighSurrogate = (code: number) => code >= 0xd800 && code <= 0xdbff
const isLowSurrogate = (code: number) => code >= 0xdc00 && code <= 0xdfff

/** Ortak önek ve sonek dışında kalan aralık; surrogate çifti bölünmez. */
export function changedRange(current: string, next: string) {
  const max = Math.min(current.length, next.length)
  let prefix = 0
  while (prefix < max && current.charCodeAt(prefix) === next.charCodeAt(prefix)) prefix++
  if (prefix > 0 && isHighSurrogate(current.charCodeAt(prefix - 1))) prefix--

  let suffix = 0
  while (
    suffix < max - prefix
    && current.charCodeAt(current.length - 1 - suffix) === next.charCodeAt(next.length - 1 - suffix)
  ) suffix++
  if (suffix > 0 && isLowSurrogate(current.charCodeAt(current.length - suffix))) suffix--

  return { start: prefix, end: current.length - suffix, text: next.slice(prefix, next.length - suffix) }
}

/** Eski `[oldStart, oldEnd)` satırları yeni `[newStart, newEnd)` satırlarıyla değişir (0 tabanlı). */
export interface LineHunk { oldStart: number; oldEnd: number; newStart: number; newEnd: number }

/** Bu sayıdan çok aralık (ör. tüm belge yeniden biçimlenmiş) tek aralığa indirilir. */
const MAX_HUNKS = 64

/**
 * Doğrusal satır farkı: iki metinde de tek geçen satırlar sırayı bozmadan çapa olur, çapa aralarında ortak baş ve son
 * satırlar atılır. En küçük fark değildir (taşınan satır iki aralık üretir) ama sonuç her zaman `next` metnini verir.
 */
export function lineHunks(oldLines: string[], newLines: string[]): LineHunk[] {
  const counts = new Map<string, { old: number; new: number; at: number }>()
  const entry = (line: string) => {
    let found = counts.get(line)
    if (!found) counts.set(line, found = { old: 0, new: 0, at: 0 })
    return found
  }
  oldLines.forEach((line) => { entry(line).old++ })
  newLines.forEach((line, i) => { const e = entry(line); e.new++; e.at = i })

  const hunks: LineHunk[] = []
  let oldPos = 0
  let newPos = 0
  const closeGap = (oldStop: number, newStop: number) => {
    while (oldPos < oldStop && newPos < newStop && oldLines[oldPos] === newLines[newPos]) { oldPos++; newPos++ }
    let oldEnd = oldStop
    let newEnd = newStop
    while (oldEnd > oldPos && newEnd > newPos && oldLines[oldEnd - 1] === newLines[newEnd - 1]) { oldEnd--; newEnd-- }
    if (oldPos < oldEnd || newPos < newEnd) hunks.push({ oldStart: oldPos, oldEnd, newStart: newPos, newEnd })
  }
  oldLines.forEach((line, i) => {
    const anchor = counts.get(line)!
    if (anchor.old !== 1 || anchor.new !== 1 || anchor.at < newPos) return
    closeGap(i, anchor.at)
    oldPos = i + 1
    newPos = anchor.at + 1
  })
  closeGap(oldLines.length, newLines.length)

  if (hunks.length <= MAX_HUNKS) return hunks
  const last = hunks[hunks.length - 1]
  return [{ oldStart: hunks[0].oldStart, oldEnd: last.oldEnd, newStart: hunks[0].newStart, newEnd: last.newEnd }]
}

/** Satır aralığını model düzenlemesine çevirir; aralık içinde yalnız gerçekten değişen karakterler yazılır. */
function hunkEdit(model: MonacoEditor.ITextModel, hunk: LineHunk, newLines: string[]): MonacoEditor.IIdentifiedSingleEditOperation {
  const eol = model.getEOL()
  const lastLine = model.getLineCount()
  const added = newLines.slice(hunk.newStart, hunk.newEnd)
  const beforeEof = hunk.oldEnd < lastLine
  // Belge sonunda arkasında satır sonu yoktur: aralık önceki satırın sonundan başlar.
  const startsAtPreviousEol = !beforeEof && hunk.oldStart > 0
  const range = {
    startLineNumber: startsAtPreviousEol ? hunk.oldStart : hunk.oldStart + 1,
    startColumn: startsAtPreviousEol ? model.getLineMaxColumn(hunk.oldStart) : 1,
    endLineNumber: beforeEof ? hunk.oldEnd + 1 : lastLine,
    endColumn: beforeEof ? 1 : model.getLineMaxColumn(lastLine),
  }
  const replacement = beforeEof
    ? added.map((line) => line + eol).join('')
    : startsAtPreviousEol ? added.map((line) => eol + line).join('') : added.join(eol)

  const base = model.getOffsetAt({ lineNumber: range.startLineNumber, column: range.startColumn })
  const { start, end, text } = changedRange(model.getValueInRange(range), replacement)
  const from = model.getPositionAt(base + start)
  const to = model.getPositionAt(base + end)
  return {
    range: { startLineNumber: from.lineNumber, startColumn: from.column, endLineNumber: to.lineNumber, endColumn: to.column },
    text,
    // Saf satır eklemede bu konumdaki imleç aşağıdaki satırla birlikte kayar; yoksa eklenen metnin başında kalırdı.
    forceMoveMarkers: beforeEof && hunk.oldStart === hunk.oldEnd,
  }
}

/**
 * Dışarıdan gelen metni yalnız değişen satır aralıklarına yazar: aynı yazım iki bölgeyi değiştirse de imleç, seçim ve
 * kaydırma Monaco'nun işaretleriyle aynı içeriğin üzerinde kalır; tek geri-al adımıdır. Satır sonları modelinkine
 * çevrilerek karşılaştırılır: model karışık satır sonunu zaten normalleştirir, fark olarak görülse tüm belge değişmiş
 * sayılırdı. Yazım süresince `writing` açıktır; içerik dinleyicileri bunu kullanıcı düzenlemesinden ayırır.
 */
export function writeExternalValue(editor: MonacoEditor.ICodeEditor, value: string, writing: RefObject<boolean>) {
  const model = editor.getModel()
  if (!model) return
  const current = model.getValue()
  if (value === current) return
  const eol = model.getEOL()
  const next = value.replace(/\r\n|\r|\n/g, eol)
  if (next === current) return

  const newLines = next.split(eol)
  const hunks = lineHunks(current.split(eol), newLines)
  const edits = hunks.map((hunk) => hunkEdit(model, hunk, newLines))
  const topLine = editor.getVisibleRanges()[0]?.startLineNumber
  const topOffset = topLine === undefined ? 0 : editor.getScrollTop() - editor.getTopForLineNumber(topLine)

  writing.current = true
  try {
    editor.pushUndoStop()
    editor.executeEdits('external-value', edits)
    editor.pushUndoStop()
  } finally {
    writing.current = false
  }

  // Görünen alanın üstündeki değişiklikte Monaco piksel konumunu korur, içerik kayar: aynı içerik görünür kalsın.
  if (topLine === undefined) return
  const above = hunks.filter((hunk) => hunk.oldEnd < topLine)
  if (above.length === 0) return
  const shift = above.reduce((sum, hunk) => sum + (hunk.newEnd - hunk.newStart) - (hunk.oldEnd - hunk.oldStart), 0)
  editor.setScrollTop(editor.getTopForLineNumber(topLine + shift) + topOffset)
}
