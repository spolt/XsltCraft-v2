import type { ReactNode } from 'react'
import AuthBackdrop from './AuthBackdrop'
import './authLayout.css'

interface AuthLayoutProps {
  children: ReactNode
}

function BrandLogo({ size = 'lg' }: { size?: 'md' | 'lg' }) {
  const lg = size === 'lg'
  return (
    <div className="flex items-center gap-4 select-none">
      <div className={`xc-auth-logo-badge grid place-items-center rounded-2xl ${lg ? 'w-16 h-16' : 'w-12 h-12'}`}>
        <svg
          width={lg ? 38 : 28}
          height={lg ? 38 : 28}
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.4"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="text-white"
          aria-hidden="true"
        >
          <path d="M8.5 7 4.5 12l4 5" />
          <path d="M15.5 7 19.5 12l-4 5" />
          <path d="M13.4 5.4 10.6 18.6" />
        </svg>
      </div>
      <span className={`font-extrabold tracking-tight leading-none ${lg ? 'text-[38px]' : 'text-[30px]'}`}>
        <span className="xc-auth-logo-word">Xslt</span>
        <span className="xc-auth-logo-accent">Craft</span>
      </span>
    </div>
  )
}

export default function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <div className="relative h-full overflow-y-auto overflow-x-hidden">
      <AuthBackdrop />

      {/* Sol üst marka logosu (geniş ekran) */}
      <div className="absolute top-16 left-20 z-20 hidden xl:block">
        <BrandLogo />
      </div>

      <main className="relative z-10 min-h-full flex flex-col items-center justify-center px-4 py-10">
        {/* Dar ekranda logo kartın üstünde */}
        <div className="xl:hidden flex justify-center mb-7">
          <BrandLogo size="md" />
        </div>

        <div data-auth-card className="xc-auth-card w-full max-w-[440px] p-6 sm:p-9">
          {children}
        </div>

        <footer className="xc-auth-footer mt-6 text-center">
          <p>
            Created by <strong>Semih Polat</strong>
          </p>
          <p className="mt-0.5">XsltCraft 2026</p>
        </footer>
      </main>
    </div>
  )
}
