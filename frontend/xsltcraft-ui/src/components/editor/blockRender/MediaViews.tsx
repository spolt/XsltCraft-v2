import { useMemo } from 'react'
import { ImageIcon } from 'lucide-react'
import type { GibKarekodBlockConfig, GibLogoBlockConfig, ImageBlockConfig } from '../../../types/blocks'
import { getAssetUrl } from '../../../services/assetService'
import { evalString, textOf, type XmlContext } from './xmlEval'
import { DOC } from './invoiceStyle'

const JUSTIFY = { left: 'flex-start', center: 'center', right: 'flex-end' } as const

/** Stilize karekod (21×21 modül) — gerçek QR çıktıda qrcode.js ile üretilir. */
const QR_PATH =
  'M0 0h7v7H0zM1 1h5v5H1zM2 2h3v3H2zM14 0h7v7h-7zM15 1h5v5h-5zM16 2h3v3h-3zM0 14h7v7H0zM1 15h5v5H1zM2 16h3v3H2z' +
  'M8 0h2v1H8zM11 1h2v2h-2zM9 3h1v2H9zM8 6h3v1H8zM12 5h1v3h-1zM0 8h2v1H0zM3 9h3v1H3zM8 8h2v2H8zM11 9h3v1h-3z' +
  'M15 8h2v2h-2zM18 9h3v1h-3zM1 11h2v2H1zM5 11h3v1H5zM9 11h1v3H9zM12 12h2v1h-2zM16 11h1v2h-1zM19 12h2v2h-2z' +
  'M8 15h3v1H8zM12 15h1v3h-1zM14 14h3v1h-3zM18 15h1v2h-1zM8 18h2v3H8zM11 19h3v1h-3zM15 17h2v2h-2zM19 19h2v2h-2z'

export function QrPlaceholder({ width, height, label, opacity }: { width: number; height: number; label: string; opacity?: number }) {
  return (
    <svg
      role="img"
      aria-label={label}
      width={width}
      height={height}
      viewBox="0 0 21 21"
      shapeRendering="crispEdges"
      style={{ flex: 'none', opacity: opacity !== undefined && opacity < 100 ? opacity / 100 : undefined }}
    >
      <path fillRule="evenodd" fill={DOC.qrInk} d={QR_PATH} />
    </svg>
  )
}

export function GibKarekodView({ config }: { config: GibKarekodBlockConfig }) {
  return (
    <div style={{ display: 'flex', justifyContent: JUSTIFY[config.qrAlignment ?? 'right'] }}>
      <QrPlaceholder width={config.qrWidth || 150} height={config.qrHeight || 150} label="GİB karekodu" opacity={config.opacity} />
    </div>
  )
}

/** GİB logosu: stilize daire + belge türü etiketi (ProfileID'ye göre, generator'daki gibi). */
export function GibLogoView({ config, ctx, despatch }: { config: GibLogoBlockConfig; ctx: XmlContext | null; despatch: boolean }) {
  const caption = useMemo(() => {
    if (despatch) return 'e-İRSALİYE'
    const profile = textOf(evalString(ctx, '/n1:Invoice/cbc:ProfileID'))
    if (profile === 'EARSIVFATURA' || profile === 'EBELGE') return 'e-Arşiv Fatura'
    if (profile === 'EGIDERPUSULASI') return 'e-Gider Pusulası'
    return 'e-FATURA'
  }, [ctx, despatch])
  const op = config.opacity
  return (
    <div style={{ display: 'flex', justifyContent: JUSTIFY[config.alignment ?? 'center'], opacity: op !== undefined && op < 100 ? op / 100 : undefined }}>
      <div style={{ textAlign: 'center' }}>
        <div
          aria-hidden="true"
          style={{
            width: config.width || '80px',
            height: config.height || '80px',
            margin: '0 auto',
            border: `1px solid ${DOC.placeholderLine}`,
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 9,
            color: DOC.placeholderInk,
          }}
        >
          GİB logo
        </div>
        <div style={{ fontSize: config.fontSize || '11px', fontWeight: 'bold', marginTop: 2, letterSpacing: '0.06em' }}>{caption}</div>
      </div>
    </div>
  )
}

export function ImageView({ config }: { config: ImageBlockConfig }) {
  const op = config.opacity
  const style = {
    display: 'inline-block',
    verticalAlign: 'top',
    objectFit: 'contain' as const,
    width: config.width || undefined,
    maxWidth: '100%',
    height: config.height || undefined,
    opacity: op !== undefined && op < 100 ? op / 100 : undefined,
  }
  return (
    <div style={{ textAlign: config.alignment ?? 'left', lineHeight: 0 }}>
      {config.assetId ? (
        <img src={getAssetUrl(config.assetId)} alt={config.altText || config.assetType} draggable={false} style={style} />
      ) : (
        <div
          role="img"
          aria-label="Görsel yer tutucu"
          style={{
            ...style,
            width: config.width || '100%',
            height: config.height || '60px',
            border: `1px dashed ${DOC.placeholderLine}`,
            display: 'inline-flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
            color: DOC.placeholderInk,
            fontSize: 10,
            lineHeight: 1.2,
          }}
        >
          <ImageIcon size={16} strokeWidth={1.6} aria-hidden="true" />
          {config.assetType === 'signature' ? 'İmza' : config.assetType === 'logo' ? 'Logo' : 'Görsel'}
        </div>
      )}
    </div>
  )
}
