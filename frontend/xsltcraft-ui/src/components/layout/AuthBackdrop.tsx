import invoiceMockup from '../../assets/loginpage_invoice_design.png'

const NOISE_SVG =
  "data:image/svg+xml,%3Csvg viewBox='0 0 220 220' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E"

const HI = 'var(--ui-auth-form-hi)'
const MID = 'var(--ui-auth-form-mid)'
const DEEP = 'var(--ui-auth-form-deep)'

function Blur({ id, sd }: { id: string; sd: number }) {
  return (
    <filter id={id} x="-50%" y="-50%" width="200%" height="200%" colorInterpolationFilters="sRGB">
      <feGaussianBlur stdDeviation={sd} />
    </filter>
  )
}

/** Akışkan kurdele: üst üste bulanık şeritlerle (gölge gövde → orta ton → üst sırt ışığı) tüp hissi. */
function Ribbon() {
  const spine = 'M -80 640 C 200 600 380 400 620 390 S 960 520 1160 430 S 1380 220 1500 180'
  const echo = 'M -80 730 C 220 700 430 510 650 505 S 1010 610 1210 525 S 1410 330 1500 300'
  return (
    <div className="xc-auth-form xc-auth-form--ribbon" style={{ left: '-10%', top: '14%', width: '120%', height: '76%', opacity: 0.8 }}>
      <svg viewBox="0 0 1400 800" preserveAspectRatio="xMidYMid slice">
        <defs>
          <linearGradient id="xca-rib-body" gradientUnits="userSpaceOnUse" x1="0" y1="640" x2="1400" y2="200">
            <stop offset="0" stopColor={DEEP} stopOpacity="0" />
            <stop offset="0.18" stopColor={DEEP} />
            <stop offset="0.42" stopColor={MID} />
            <stop offset="0.6" stopColor={DEEP} />
            <stop offset="0.82" stopColor={MID} />
            <stop offset="1" stopColor={DEEP} stopOpacity="0" />
          </linearGradient>
          <linearGradient id="xca-rib-crest" gradientUnits="userSpaceOnUse" x1="0" y1="640" x2="1400" y2="200">
            <stop offset="0.08" stopColor={HI} stopOpacity="0" />
            <stop offset="0.38" stopColor={HI} stopOpacity="0.85" />
            <stop offset="0.55" stopColor={HI} stopOpacity="0.15" />
            <stop offset="0.8" stopColor={HI} stopOpacity="0.7" />
            <stop offset="0.96" stopColor={HI} stopOpacity="0" />
          </linearGradient>
          <Blur id="xca-b4" sd={4} />
          <Blur id="xca-b10" sd={10} />
          <Blur id="xca-b36" sd={36} />
        </defs>
        <path d={spine} fill="none" stroke={MID} strokeOpacity="0.35" strokeWidth="190" strokeLinecap="round" filter="url(#xca-b36)" />
        <path d={spine} fill="none" stroke="url(#xca-rib-body)" strokeWidth="128" strokeLinecap="round" filter="url(#xca-b4)" />
        <path d={spine} fill="none" stroke="url(#xca-rib-body)" strokeWidth="64" strokeLinecap="round" transform="translate(0 -18)" filter="url(#xca-b10)" style={{ mixBlendMode: 'screen' }} />
        <path d={spine} fill="none" stroke="url(#xca-rib-crest)" strokeWidth="16" strokeLinecap="round" transform="translate(0 -44)" filter="url(#xca-b10)" />
        <path d={echo} fill="none" stroke="url(#xca-rib-crest)" strokeOpacity="0.55" strokeWidth="5" strokeLinecap="round" filter="url(#xca-b4)" />
      </svg>
    </div>
  )
}

/** Yumuşak torus: eğik halka; ışık sol üstten, iç boşlukta gölge. */
function Torus() {
  return (
    <div
      className="xc-auth-form xc-auth-form--torus"
      style={{ right: '-6%', top: '-11%', width: 'clamp(240px, 31vw, 520px)', aspectRatio: '1', opacity: 0.5 }}
    >
      <svg viewBox="0 0 600 600">
        <defs>
          <radialGradient id="xca-tor" gradientUnits="userSpaceOnUse" cx="210" cy="170" r="470">
            <stop offset="0" stopColor={HI} />
            <stop offset="0.38" stopColor={MID} />
            <stop offset="0.78" stopColor={DEEP} />
          </radialGradient>
          <Blur id="xca-t2" sd={2} />
          <Blur id="xca-t12" sd={12} />
          <Blur id="xca-t48" sd={48} />
        </defs>
        <g transform="translate(300 300) scale(1 0.62) translate(-300 -300)">
          <circle cx="300" cy="300" r="190" fill="none" stroke={MID} strokeOpacity="0.4" strokeWidth="200" filter="url(#xca-t48)" />
          <circle cx="300" cy="300" r="190" fill="none" stroke="url(#xca-tor)" strokeWidth="150" filter="url(#xca-t2)" />
          <circle cx="300" cy="300" r="120" fill="none" stroke={DEEP} strokeOpacity="0.85" strokeWidth="26" filter="url(#xca-t12)" />
          <circle cx="292" cy="280" r="205" fill="none" stroke={HI} strokeOpacity="0.32" strokeWidth="14" filter="url(#xca-t12)" />
        </g>
      </svg>
    </div>
  )
}

/** Organik damla: zemine eriyen kenar, sol üst ışık + sağ alt yansıyan ışık. */
function Drop() {
  return (
    <div
      className="xc-auth-form xc-auth-form--drop"
      style={{ right: '-7%', bottom: '-22%', width: 'clamp(280px, 34vw, 580px)', aspectRatio: '1', opacity: 0.48 }}
    >
      <svg viewBox="0 0 600 600">
        <defs>
          <radialGradient id="xca-drop" gradientUnits="objectBoundingBox" cx="0.36" cy="0.3" r="0.78">
            <stop offset="0" stopColor={HI} stopOpacity="0.9" />
            <stop offset="0.35" stopColor={MID} />
            <stop offset="0.8" stopColor={DEEP} />
          </radialGradient>
          <radialGradient id="xca-drop-rim" gradientUnits="objectBoundingBox" cx="0.78" cy="0.82" r="0.45">
            <stop offset="0" stopColor={HI} stopOpacity="0.35" />
            <stop offset="1" stopColor={HI} stopOpacity="0" />
          </radialGradient>
          <Blur id="xca-d6" sd={6} />
          <Blur id="xca-d56" sd={56} />
        </defs>
        <path
          d="M 318 52 C 452 66 548 176 538 318 C 528 462 418 556 284 548 C 154 540 58 448 64 318 C 70 192 186 40 318 52 Z"
          fill={MID}
          fillOpacity="0.45"
          filter="url(#xca-d56)"
        />
        <g filter="url(#xca-d6)">
          <path d="M 318 52 C 452 66 548 176 538 318 C 528 462 418 556 284 548 C 154 540 58 448 64 318 C 70 192 186 40 318 52 Z" fill="url(#xca-drop)" />
          <path d="M 318 52 C 452 66 548 176 538 318 C 528 462 418 556 284 548 C 154 540 58 448 64 318 C 70 192 186 40 318 52 Z" fill="url(#xca-drop-rim)" />
        </g>
      </svg>
    </div>
  )
}

/** Giriş/Kayıt zemini: katmanlı tema zemini + akışkan 3B formlar + fatura görseli + grain. */
export default function AuthBackdrop() {
  return (
    <div className="xc-auth-backdrop" aria-hidden="true">
      <Ribbon />
      <Torus />
      <Drop />
      <div className="xc-auth-veil" />
      <div className="xc-auth-grain" style={{ backgroundImage: `url("${NOISE_SVG}")` }} />

      {/* Fatura görseli: geniş ekranda sol alt; orta genişlikte sol üstte küçük ve soluk; telefonda gizli */}
      <img
        src={invoiceMockup}
        alt=""
        className="xc-auth-invoice hidden xl:block bottom-10 left-20 w-[480px] 2xl:w-[520px]"
      />
      <img
        src={invoiceMockup}
        alt=""
        className="xc-auth-invoice hidden sm:block xl:hidden top-4 left-4 w-[220px]"
        style={{ opacity: 0.5 }}
      />
    </div>
  )
}
