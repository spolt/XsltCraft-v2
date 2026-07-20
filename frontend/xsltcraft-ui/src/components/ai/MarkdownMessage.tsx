import React, { useMemo, useState } from 'react'
import Editor from '@monaco-editor/react'
import { Copy, ClipboardCheck } from 'lucide-react'
import {
  parseSegments, parseBlocks, type Block, type Inline,
} from '../../utils/markdownLite'

// ─── Satır içi ───────────────────────────────────────────────────────────────

function InlineNodes({ nodes }: { nodes: Inline[] }) {
  return (
    <>
      {nodes.map((n, i) => {
        switch (n.t) {
          case 'text':
            return <React.Fragment key={i}>{n.v}</React.Fragment>
          case 'strong':
            return <strong key={i} className="font-semibold text-white"><InlineNodes nodes={n.c} /></strong>
          case 'em':
            return <em key={i} className="italic"><InlineNodes nodes={n.c} /></em>
          case 'code':
            return (
              <code
                key={i}
                className="px-1 py-0.5 rounded bg-gray-900 border border-gray-700 font-mono text-[12px] text-violet-300 break-words"
              >
                {n.v}
              </code>
            )
          case 'link':
            // href markdownLite'ta http/https/mailto ile sınırlandı (javascript:/data: reddedilir).
            return (
              <a
                key={i}
                href={n.href}
                target="_blank"
                rel="noopener noreferrer"
                className="text-violet-400 underline hover:text-violet-300"
              >
                <InlineNodes nodes={n.c} />
              </a>
            )
        }
      })}
    </>
  )
}

// ─── Blok düzeyi ─────────────────────────────────────────────────────────────

// Panel dar; başlıkları makul ölçekte tut (h1'i devasa yapma).
const H_CLASS: Record<1 | 2 | 3, string> = {
  1: 'text-sm font-semibold text-white mt-2 mb-0.5',
  2: 'text-[13px] font-semibold text-gray-100 mt-2 mb-0.5',
  3: 'text-[13px] font-medium text-gray-200 mt-1.5 mb-0.5',
}

function BlockNodes({ blocks }: { blocks: Block[] }) {
  return (
    <>
      {blocks.map((b, i) => {
        switch (b.t) {
          case 'h': {
            const Tag = (['h1', 'h2', 'h3'] as const)[b.level - 1]
            return <Tag key={i} className={H_CLASS[b.level]}><InlineNodes nodes={b.c} /></Tag>
          }
          case 'p':
            return (
              <p key={i} className="text-sm text-gray-200 leading-relaxed break-words">
                {b.lines.map((ln, j) => (
                  <React.Fragment key={j}>
                    {j > 0 && <br />}
                    <InlineNodes nodes={ln} />
                  </React.Fragment>
                ))}
              </p>
            )
          case 'ul':
            return (
              <ul key={i} className="list-disc pl-5 space-y-0.5 text-sm text-gray-200 leading-relaxed">
                {b.items.map((it, j) => <li key={j} className="break-words"><InlineNodes nodes={it} /></li>)}
              </ul>
            )
          case 'ol':
            return (
              <ol key={i} start={b.start} className="list-decimal pl-5 space-y-0.5 text-sm text-gray-200 leading-relaxed">
                {b.items.map((it, j) => <li key={j} className="break-words"><InlineNodes nodes={it} /></li>)}
              </ol>
            )
          case 'hr':
            return <hr key={i} className="border-gray-700 my-2" />
        }
      })}
    </>
  )
}

// ─── Kod bloğu ───────────────────────────────────────────────────────────────

function toMonacoLang(lang: string): string {
  if (lang === 'xslt' || lang === 'xml' || lang === 'html') return 'xml'
  if (lang === 'json') return 'json'
  return 'plaintext'
}

// Monaco ile <pre> arasında geçiş görünmez olsun diye ikisi de bu ölçüleri kullanır.
const CODE_FONT_SIZE = 13
const CODE_LINE_HEIGHT = 19

function CodeBlock({ lang, code, open }: { lang: string; code: string; open: boolean }) {
  const [copied, setCopied] = useState(false)

  // Kopyala kalıbı AiApplyDialog.tsx:58-64 ile aynı.
  async function copy() {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch { /* ignore */ }
  }

  const height = Math.min(Math.max(code.split('\n').length * CODE_LINE_HEIGHT + 18, 48), 320)

  return (
    <div className="rounded overflow-hidden border border-gray-700">
      <div className="px-2 py-0.5 bg-gray-800 border-b border-gray-700 flex items-center gap-2">
        <span className="text-[10px] text-gray-400 font-mono">{lang || 'kod'}</span>
        <div className="flex-1" />
        {/* Akış sürerken kopyalama gizli — yarım kod kopyalatmak yanıltıcı olur. */}
        {!open && (
          <button
            onClick={copy}
            className="flex items-center gap-1 text-[10px] text-gray-400 hover:text-gray-200 transition-colors"
            title="Kodu panoya kopyala"
          >
            {copied ? <ClipboardCheck size={11} className="text-emerald-400" /> : <Copy size={11} />}
            {copied ? 'Kopyalandı' : 'Kopyala'}
          </button>
        )}
      </div>

      {open ? (
        // Yazılmakta olan blokta Monaco kullanma: her token'da setValue + layout yapar,
        // yükseklik zıplar ve scroll'un dibe yapışması bozulur. Blok kapanınca Monaco'ya geçilir.
        <pre
          className="px-3 py-2 bg-[#1e1e1e] font-mono text-gray-200 overflow-x-auto whitespace-pre"
          style={{ fontSize: CODE_FONT_SIZE, lineHeight: `${CODE_LINE_HEIGHT}px` }}
        >
          {code}
        </pre>
      ) : (
        <Editor
          height={height}
          language={toMonacoLang(lang)}
          value={code}
          theme="vs-dark"
          loading={<div className="bg-gray-900 animate-pulse" style={{ height }} />}
          options={{
            readOnly: true,
            minimap: { enabled: false },
            lineNumbers: 'off',
            folding: false,
            fontSize: CODE_FONT_SIZE,
            lineHeight: CODE_LINE_HEIGHT,
            wordWrap: 'on',
            scrollBeyondLastLine: false,
            scrollbar: { vertical: 'auto', horizontal: 'hidden', alwaysConsumeMouseWheel: false },
            overviewRulerLanes: 0,
            renderLineHighlight: 'none',
            contextmenu: false,
            padding: { top: 8, bottom: 8 },
          }}
        />
      )}
    </div>
  )
}

// ─── Dışa açılan bileşen ─────────────────────────────────────────────────────

/**
 * Bir asistan yanıtını render eder.
 *
 * React.memo + useMemo ZORUNLU: streaming sırasında her delta'da `messages` dizisi yeni
 * referans alır ve memo olmasa TÜM mesajlar her token'da yeniden ayrıştırılırdı (20
 * mesajlık sohbette token başına 20 tam ayrıştırma). Yalnız primitive prop alır.
 */
const MarkdownMessage = React.memo(function MarkdownMessage({ text }: { text: string }) {
  const segments = useMemo(() => parseSegments(text), [text])

  return (
    <div className="space-y-2">
      {segments.map((seg, i) =>
        seg.kind === 'code' ? (
          <CodeBlock key={i} lang={seg.lang} code={seg.content} open={seg.open} />
        ) : (
          <div key={i} className="space-y-1.5">
            <BlockNodes blocks={parseBlocks(seg.content)} />
          </div>
        )
      )}
    </div>
  )
})

export default MarkdownMessage
