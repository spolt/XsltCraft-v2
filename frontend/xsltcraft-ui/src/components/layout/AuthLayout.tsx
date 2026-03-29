import type { ReactNode } from 'react'

export type AuthTheme = 'A' | 'B' | 'C'

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

function Footer() {
  return (
    <div className="relative z-10 mt-5 text-center select-none">
      <p className="text-xs text-gray-600">
        Created by <span className="text-gray-500 font-medium">Semih Polat</span>
      </p>
      <p className="text-xs text-gray-700 mt-0.5">XsltCraft 2026</p>
    </div>
  )
}

export default function AuthLayout({ children, theme = 'A' }: AuthLayoutProps) {
  if (theme === 'B') return <ThemeB>{children}</ThemeB>
  if (theme === 'C') return <ThemeC>{children}</ThemeC>
  return <ThemeA>{children}</ThemeA>
}
