import type { ReactNode } from 'react'
import invoiceMockup from '../../assets/loginpage_invoice_design.png'

export type AuthTheme = 'A' | 'B' | 'C' | 'D'

interface AuthLayoutProps {
  children: ReactNode
  theme?: AuthTheme
}

const NOISE_SVG =
  "data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)'/%3E%3C/svg%3E"

function ThemeA({ children }: { children: ReactNode }) {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center relative overflow-hidden"
      style={{
        background:
          'radial-gradient(ellipse at 20% 50%, #312e81 0%, transparent 55%), radial-gradient(ellipse at 80% 20%, #4c1d95 0%, transparent 55%), #030712',
      }}
    >
      {/* Animasyonlu blur blob'lar */}
      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-indigo-600/20 rounded-full blur-3xl animate-pulse pointer-events-none" />
      <div
        className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-purple-600/15 rounded-full blur-3xl animate-pulse pointer-events-none"
        style={{ animationDelay: '1.2s' }}
      />
      <div
        className="absolute top-3/4 left-1/2 w-64 h-64 bg-violet-700/10 rounded-full blur-3xl animate-pulse pointer-events-none"
        style={{ animationDelay: '2.4s' }}
      />

      {/* Glassmorphism kart */}
      <div className="relative z-10 w-full max-w-md mx-4 backdrop-blur-xl bg-white/5 border border-white/10 rounded-2xl shadow-2xl p-8">
        {children}
      </div>

      <Footer />
    </div>
  )
}

function ThemeB({ children }: { children: ReactNode }) {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center relative overflow-hidden"
      style={{
        backgroundColor: '#030712',
        backgroundImage:
          'linear-gradient(rgba(99,102,241,0.07) 1px, transparent 1px), linear-gradient(90deg, rgba(99,102,241,0.07) 1px, transparent 1px)',
        backgroundSize: '40px 40px',
      }}
    >
      {/* Merkez radyal glow */}
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
        <div className="w-[640px] h-[420px] bg-indigo-600/10 rounded-full blur-[110px]" />
      </div>

      {/* Kart: glow border */}
      <div className="relative z-10 w-full max-w-md mx-4 bg-gray-900 border border-indigo-500/30 rounded-2xl shadow-[0_0_40px_rgba(99,102,241,0.15)] p-8">
        {children}
      </div>

      <Footer />
    </div>
  )
}

function ThemeC({ children }: { children: ReactNode }) {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center relative overflow-hidden"
      style={{ background: 'linear-gradient(135deg, #020617 0%, #0f0a1e 50%, #020617 100%)' }}
    >
      {/* Noise texture overlay */}
      <div
        className="absolute inset-0 opacity-[0.035] pointer-events-none"
        style={{ backgroundImage: `url("${NOISE_SVG}")` }}
      />

      {/* Dekoratif köşe çizgileri */}
      <div className="absolute top-8 left-8 w-16 h-16 border-t border-l border-indigo-500/20 rounded-tl-lg pointer-events-none" />
      <div className="absolute bottom-8 right-8 w-16 h-16 border-b border-r border-indigo-500/20 rounded-br-lg pointer-events-none" />

      {/* Kart: gradient üst çizgi */}
      <div className="relative z-10 w-full max-w-md mx-4">
        <div className="absolute top-0 left-10 right-10 h-px bg-gradient-to-r from-transparent via-indigo-500/60 to-transparent" />
        <div className="bg-gray-900/95 rounded-2xl shadow-2xl p-8 border border-gray-800">
          {children}
        </div>
      </div>

      <Footer />
    </div>
  )
}

// Görseldeki gibi sağ tarafta yoğunlaşan, parlak mor küreler.
// Her küre: konum (sol %), dikey (üst %), çap (px), mor tonu, opaklık.
type Bubble = { left: number; top: number; size: number; hue: string; opacity: number }

const BUBBLES: Bubble[] = [
  { left: 95, top: 8, size: 60, hue: '#c9a8e8', opacity: 0.9 },
  { left: 82, top: 14, size: 90, hue: '#9b6fc4', opacity: 0.85 },
  { left: 70, top: 6, size: 34, hue: '#b894d8', opacity: 0.7 },
  { left: 88, top: 30, size: 120, hue: '#a878cf', opacity: 0.95 },
  { left: 64, top: 24, size: 46, hue: '#7e57a6', opacity: 0.6 },
  { left: 97, top: 42, size: 70, hue: '#d6bbe8', opacity: 0.85 },
  { left: 76, top: 40, size: 100, hue: '#8b5fb0', opacity: 0.9 },
  { left: 58, top: 18, size: 26, hue: '#c9a8e8', opacity: 0.55 },
  { left: 90, top: 56, size: 84, hue: '#a878cf', opacity: 0.88 },
  { left: 68, top: 54, size: 54, hue: '#9b6fc4', opacity: 0.75 },
  { left: 80, top: 68, size: 110, hue: '#7e57a6', opacity: 0.92 },
  { left: 96, top: 72, size: 40, hue: '#d6bbe8', opacity: 0.8 },
  { left: 60, top: 44, size: 30, hue: '#b894d8', opacity: 0.6 },
  { left: 86, top: 84, size: 96, hue: '#9b6fc4', opacity: 0.9 },
  { left: 70, top: 80, size: 58, hue: '#a878cf', opacity: 0.8 },
  { left: 54, top: 64, size: 24, hue: '#c9a8e8', opacity: 0.5 },
  { left: 94, top: 92, size: 64, hue: '#8b5fb0', opacity: 0.85 },
  { left: 62, top: 90, size: 44, hue: '#b894d8', opacity: 0.7 },
  { left: 48, top: 34, size: 18, hue: '#d6bbe8', opacity: 0.45 },
  { left: 52, top: 78, size: 20, hue: '#9b6fc4', opacity: 0.5 },
]

function BrandLogo() {
  return (
    <div className="flex items-center gap-4 select-none">
      {/* Kod rozeti */}
      <div
        className="grid place-items-center w-16 h-16 rounded-2xl"
        style={{
          background: 'linear-gradient(135deg, #38bdf8 0%, #0c8ff5 50%, #0a63dd 100%)',
          boxShadow: '0 8px 26px rgba(12,143,245,0.45), inset 0 1px 0 rgba(255,255,255,0.25)',
        }}
      >
        <svg
          width="38"
          height="38"
          viewBox="0 0 24 24"
          fill="none"
          stroke="#ffffff"
          strokeWidth="2.4"
          strokeLinecap="round"
          strokeLinejoin="round"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path d="M8.5 7 4.5 12l4 5" />
          <path d="M15.5 7 19.5 12l-4 5" />
          <path d="M13.4 5.4 10.6 18.6" />
        </svg>
      </div>

      {/* Wordmark */}
      <span className="text-[38px] font-extrabold tracking-tight leading-none">
        <span className="text-white">Xslt</span>
        <span className="text-[#38bdf8]">Craft</span>
      </span>
    </div>
  )
}

function ThemeD({ children }: { children: ReactNode }) {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center relative overflow-hidden"
      style={{
        background:
          'radial-gradient(ellipse at 30% 45%, #6e6a73 0%, #565259 45%, #3b383f 100%)',
      }}
    >
      {/* Sol üst marka logosu */}
      <div className="absolute top-20 left-28 z-20 hidden xl:block">
        <BrandLogo />
      </div>

      {/* Sol alt fatura tasarımı görseli — kenarları sayfaya karışsın diye maskelendi */}
      <img
        src={invoiceMockup}
        alt="XsltCraft e-Fatura tasarımı"
        className="absolute bottom-16 left-24 z-0 hidden xl:block w-[480px] pointer-events-none select-none"
        style={{
          WebkitMaskImage:
            'radial-gradient(ellipse 66% 66% at 50% 46%, #000 38%, transparent 82%)',
          maskImage: 'radial-gradient(ellipse 66% 66% at 50% 46%, #000 38%, transparent 82%)',
          filter: 'drop-shadow(0 24px 38px rgba(0,0,0,0.4))',
        }}
      />

      {/* Dağınık mor küreler */}
      <div className="absolute inset-0 pointer-events-none">
        {BUBBLES.map((b, i) => (
          <span
            key={i}
            className="absolute rounded-full"
            style={{
              left: `${b.left}%`,
              top: `${b.top}%`,
              width: b.size,
              height: b.size,
              opacity: b.opacity,
              transform: 'translate(-50%, -50%)',
              background: `radial-gradient(circle at 32% 28%, #f1e6fb 0%, ${b.hue} 42%, ${b.hue}cc 70%, ${b.hue}77 100%)`,
              boxShadow: `0 8px 24px ${b.hue}55`,
            }}
          />
        ))}
      </div>

      {/* Kart sütunu (küçük ekranda logo kartın üstünde) */}
      <div className="relative z-10 w-full max-w-md mx-4">
        {/* Küçük ekran logosu — geniş ekranda yan logo göründüğü için gizli */}
        <div className="xl:hidden flex justify-center mb-7">
          <BrandLogo />
        </div>

        {/* Glassmorphism kart */}
        <div className="backdrop-blur-xl bg-gray-900/60 border border-white/10 rounded-2xl shadow-2xl p-8">
          {children}
        </div>
      </div>

      <Footer light />

      {/* Küçük ekranda fatura görseli en üstte */}
      <img
        src={invoiceMockup}
        alt="XsltCraft e-Fatura tasarımı"
        className="xl:hidden absolute top-0 left-1/2 -translate-x-1/2 z-0 w-[280px] pointer-events-none select-none"
        style={{
          WebkitMaskImage:
            'radial-gradient(ellipse 66% 66% at 50% 46%, #000 38%, transparent 82%)',
          maskImage: 'radial-gradient(ellipse 66% 66% at 50% 46%, #000 38%, transparent 82%)',
          filter: 'drop-shadow(0 24px 38px rgba(0,0,0,0.4))',
        }}
      />
    </div>
  )
}

function Footer({ light = false }: { light?: boolean }) {
  if (light) {
    return (
      <div
        className="relative z-10 mt-5 text-center"
        style={{ textShadow: '0 1px 3px rgba(0,0,0,0.45)' }}
      >
        <p className="text-sm text-gray-200">
          Created by <span className="text-white font-semibold">Semih Polat</span>
        </p>
        <p className="text-sm text-gray-300 mt-0.5">XsltCraft 2026</p>
      </div>
    )
  }
  return (
    <div className="relative z-10 mt-5 text-center">
      <p className="text-sm text-gray-500">
        Created by <span className="text-gray-400 font-medium">Semih Polat</span>
      </p>
      <p className="text-sm text-gray-600 mt-0.5">XsltCraft 2026</p>
    </div>
  )
}

export default function AuthLayout({ children, theme = 'A' }: AuthLayoutProps) {
  if (theme === 'B') return <ThemeB>{children}</ThemeB>
  if (theme === 'C') return <ThemeC>{children}</ThemeC>
  if (theme === 'D') return <ThemeD>{children}</ThemeD>
  return <ThemeA>{children}</ThemeA>
}
