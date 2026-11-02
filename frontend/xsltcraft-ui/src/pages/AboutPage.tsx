import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { BookOpen, Code2, FileCode2, LayoutGrid, Sparkles, type LucideIcon } from 'lucide-react'
import Card from '../components/ui/Card'
import IconTile from '../components/ui/IconTile'
import type { FnColor } from '../theme/uiThemes'

type Feature = {
  icon: LucideIcon
  title: string
  description: string
  color: FnColor
}

const features: Feature[] = [
  {
    icon: LayoutGrid,
    title: 'Tasarım Alanı',
    description: 'A4 ızgarada sürükle-bırak bloklar ve XPath bağlama.',
    color: 'blue',
  },
  {
    icon: Code2,
    title: 'XSLT Editör',
    description: 'Monaco tabanlı editör, canlı önizleme ve hata paneli.',
    color: 'purple',
  },
  {
    icon: BookOpen,
    title: 'Tema Kütüphanesi',
    description: 'Hazır UBL-TR şablonları (Invoice, CreditNote, DespatchAdvice).',
    color: 'green',
  },
  {
    icon: Sparkles,
    title: 'AI Asistan',
    description: 'Türkçe doğal dilde XSLT refactor ve sohbet (Ollama + Gemini yedek).',
    color: 'orange',
  },
  {
    icon: FileCode2,
    title: 'Taslaklar ve Şablonlarım',
    description: 'Kişisel kayıt, düzenleme ve sürüm takibi.',
    color: 'violet',
  },
]

const RELEASE_NOTES = [
  'XSLT 2.0 desteği — Saxon-HE motoru, izole çalışma alanında.',
  'XSLT editöründe 1.0 uyumluluk uyarıları.',
  'Temalarda XSLT 2.0 ile hazırlanmış şablonlar.',
  'Yeni arayüz: Safir, Ametist ve Gece Mavisi renkleri.',
]

export default function AboutPage() {
  const { hash } = useLocation()

  // "/about#yenilikler" (menü altındaki "Yenilikler") → sürüm notlarına kaydır.
  useEffect(() => {
    if (!hash) return
    const el = document.getElementById(decodeURIComponent(hash.slice(1)))
    if (!el) return
    const smooth = !window.matchMedia('(prefers-reduced-motion: reduce)').matches
    el.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto', block: 'start' })
    el.focus({ preventScroll: true })
  }, [hash])

  return (
    <div className="mx-auto flex w-full max-w-[960px] flex-col gap-5 px-8 pt-7 pb-9">
      <Card as="section" aria-labelledby="about-title" padding="none" className="flex flex-wrap items-center gap-[22px] p-7">
        <span
          aria-hidden="true"
          className="flex h-[72px] w-[72px] flex-none items-center justify-center rounded-2xl bg-ui-accent text-[34px] font-bold text-ui-accent-fg"
        >
          X
        </span>
        <div className="min-w-0 flex-[1_1_360px]">
          <h1 id="about-title" className="m-0 text-2xl font-semibold text-tx">XsltCraft</h1>
          <p className="mt-1.5 mb-0 text-sm leading-relaxed text-tx-muted">
            Türk e-Fatura ve e-İrsaliye (UBL-TR 2.1) için low-code XSLT şablon tasarımcısı.
          </p>
        </div>
        <div className="flex flex-col items-end gap-1.5">
          <span className="rounded-full bg-ui-soft px-3 py-1 font-bold text-ui-ink">Sürüm {__APP_VERSION__}</span>
          <span className="text-xs text-tx-muted">Saxon-HE 12.10 · XSLT 2.0</span>
        </div>
      </Card>

      <h2 className="mt-1 mb-0 text-[15px] font-semibold text-tx">Öne Çıkan Özellikler</h2>
      <ul className="m-0 grid list-none grid-cols-[repeat(auto-fit,minmax(210px,1fr))] gap-4 p-0">
        {features.map(({ icon, title, description, color }) => (
          <li key={title} className="flex">
            <Card lift fn={color} padding="none" className="flex flex-1 flex-col gap-2.5 p-[18px]">
              <IconTile icon={icon} size="lg" />
              <span className="text-sm font-semibold text-tx">{title}</span>
              <span className="text-[12.5px] leading-normal text-tx-muted">{description}</span>
            </Card>
          </li>
        ))}
      </ul>

      <Card
        as="section"
        id="yenilikler"
        tabIndex={-1}
        aria-labelledby="about-release-title"
        padding="none"
        className="flex scroll-mt-6 flex-col gap-2.5 p-[22px] outline-none"
      >
        <h2 id="about-release-title" className="m-0 text-[15px] font-semibold text-tx">2.0.0 sürüm notları</h2>
        <ul className="m-0 flex flex-col gap-1.5 pl-[18px] leading-relaxed text-tx-2">
          {RELEASE_NOTES.map((n) => <li key={n}>{n}</li>)}
        </ul>
      </Card>

      <p className="m-0 text-center text-xs text-tx-muted">© 2026 XsltCraft</p>
    </div>
  )
}
