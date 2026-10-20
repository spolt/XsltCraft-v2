import { useEffect, useState } from 'react'
import { CheckCircle2, CircleX, TriangleAlert, X } from 'lucide-react'
import type { AdminTheme, ThemeSlotReport as Report } from '../../services/adminService'
import { previewFromStoredXslt } from '../../services/previewService'
import type { ThemeSlotOption } from '../../services/templateService'
import ThemeSlotPreview from '../theme-assets/ThemeSlotPreview'
import defaultInvoiceXml from '../../assets/default-invoice.xml?raw'
import defaultDespatchXml from '../../assets/default-despatch.xml?raw'

const STATUS: Record<Report['status'], { label: string; cls: string }> = {
  ok: { label: 'Tamam', cls: 'bg-green-100 text-green-700' },
  partial: { label: 'Kısmi', cls: 'bg-amber-100 text-amber-700' },
  none: { label: 'Konum yok', cls: 'bg-gray-100 text-gray-600' },
  failed: { label: 'Başarısız', cls: 'bg-red-100 text-red-700' },
  skipped: { label: 'Atlandı', cls: 'bg-gray-100 text-gray-600' },
}

const DEFAULT_LABEL = { logo: 'Varsayılan logo', signature: 'Varsayılan imza' } as const

const ROLE_LABEL = { logo: 'Firma logosu', signature: 'İmza / kaşe', design: 'Tasarımın parçası' } as const

/** Admin: temanın logo/imza konum analizi — bulunan zorunlu alanlar, konumlar, uyarılar ve önizleme. */
export default function ThemeSlotReport({ theme, onClose }: { theme: AdminTheme; onClose: () => void }) {
  const report = theme.slotAnalysis
  const [html, setHtml] = useState<string | null>(null)
  const [showPreview, setShowPreview] = useState(false)

  useEffect(() => {
    if (!showPreview || html !== null) return
    const xml = theme.documentType === 'Despatch' ? defaultDespatchXml : defaultInvoiceXml
    previewFromStoredXslt(theme.id, xml).then((r) => setHtml(r.html)).catch(() => setHtml(''))
  }, [showPreview, html, theme])

  const options: ThemeSlotOption[] = (report?.slots ?? []).map((s) => ({
    id: s.id, label: s.label, group: s.group, defaultFor: s.defaultFor, order: s.order, removable: s.removable,
  }))

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" role="dialog" aria-modal="true" aria-labelledby="slot-report-title">
      <div className="bg-white rounded-xl shadow-xl w-full mx-4 max-h-[90vh] flex flex-col" style={{ maxWidth: showPreview ? 940 : 640 }}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div>
            <h3 id="slot-report-title" className="text-base font-semibold text-gray-900">Logo / imza konumları</h3>
            <p className="text-xs text-gray-500">{theme.name}</p>
          </div>
          <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-700" aria-label="Kapat"><X size={18} /></button>
        </div>

        <div className="overflow-y-auto px-6 py-4 flex flex-col gap-5">
          {!report ? (
            <p className="text-sm text-gray-500">
              Bu tema konum analizi yapılmadan önce yüklenmiş. Kullanıcılar logo/imzayı temanın eski alanına
              yerleştirebilir; konum seçimi için temayı yeniden yükleyin.
            </p>
          ) : (
            <>
              <div className="flex items-center gap-2 text-sm">
                <span className={`text-xs font-semibold rounded-full px-2 py-0.5 ${STATUS[report.status].cls}`}>{STATUS[report.status].label}</span>
                <span className="text-gray-600">{report.slots.length} konum bulundu</span>
                {report.overrides?.found && (
                  <span className="text-xs text-gray-500">
                    · önceki görseller {report.overrides.decision === 'keep' ? `korundu (${report.overrides.imagesKept})` : 'kaldırıldı'}
                  </span>
                )}
              </div>

              <section>
                <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Zorunlu alanlar</h4>
                <ul className="grid grid-cols-2 gap-x-4 gap-y-1">
                  {report.anchors.map((a) => (
                    <li key={a.id} className="flex items-center gap-1.5 text-sm">
                      {a.found
                        ? <CheckCircle2 size={14} className="text-green-600 flex-shrink-0" />
                        : <CircleX size={14} className="text-gray-300 flex-shrink-0" />}
                      <span className={a.found ? 'text-gray-800' : 'text-gray-400'}>{a.label}</span>
                      {a.line != null && <span className="text-xs text-gray-400">satır {a.line}</span>}
                    </li>
                  ))}
                </ul>
              </section>

              {report.slots.length > 0 && (
                <section>
                  <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Kullanıcıya sunulan konumlar</h4>
                  <ul className="flex flex-col divide-y divide-gray-100 border border-gray-100 rounded-lg">
                    {report.slots.map((s) => (
                      <li key={s.id} className="flex items-center gap-2 px-3 py-2 text-sm flex-wrap">
                        <span className="text-gray-800 font-medium">{s.label}</span>
                        <span className="text-xs text-gray-400">{s.group}</span>
                        {s.defaultFor && (
                          <span className="text-[11px] bg-blue-50 text-blue-700 rounded px-1.5 py-0.5">{DEFAULT_LABEL[s.defaultFor]}</span>
                        )}
                        {s.conditional && (
                          <span className="text-[11px] bg-amber-50 text-amber-700 rounded px-1.5 py-0.5" title="Bu alan her faturada çıkmayabilir">Koşullu</span>
                        )}
                        <span className="ml-auto text-xs text-gray-400">
                          {s.legacyId ? `#${s.legacyId}` : 'yeni alan'} · satır {s.line}
                        </span>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {report.images.length > 0 && (
                <section>
                  <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Temadaki görseller</h4>
                  <ul className="flex flex-col gap-1">
                    {report.images.map((img) => (
                      <li key={img.index} className="flex items-center gap-2 text-sm flex-wrap">
                        <span className="text-gray-800">{img.index + 1}. görsel</span>
                        <span className="text-xs text-gray-400">satır {img.line}</span>
                        <span className="text-[11px] bg-gray-100 text-gray-700 rounded px-1.5 py-0.5">{ROLE_LABEL[img.role]}</span>
                        {img.stripped && <span className="text-[11px] bg-red-50 text-red-700 rounded px-1.5 py-0.5">Temadan kaldırıldı</span>}
                        {img.role !== 'design' && (img.slotId
                          ? <span className="text-xs text-gray-500">
                              → kullanıcının görseli bunun yerini alır{img.removable ? ' · kullanıcı kaldırabilir' : ''}
                            </span>
                          : <span className="text-xs text-amber-700">konum doğrulanamadı; görsel olduğu gibi kalır</span>)}
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {report.warnings.length > 0 && (
                <section>
                  <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Uyarılar</h4>
                  <ul className="flex flex-col gap-1">
                    {report.warnings.map((w, i) => (
                      <li key={i} className="flex gap-1.5 text-xs text-amber-800">
                        <TriangleAlert size={12} className="flex-shrink-0 mt-0.5" />{w.message}
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              {report.slots.length > 0 && (
                <section>
                  <button
                    type="button" onClick={() => setShowPreview((v) => !v)}
                    className="text-sm text-blue-600 hover:underline"
                  >
                    {showPreview ? 'Önizlemeyi gizle' : 'Konumları önizlemede göster'}
                  </button>
                  {showPreview && (
                    <div className="mt-3 overflow-auto bg-gray-300 rounded-lg p-3 flex justify-center" style={{ maxHeight: '60vh' }}>
                      {html === null
                        ? <span className="text-sm text-gray-600 py-10">Yükleniyor…</span>
                        : <ThemeSlotPreview html={html} slots={options} showSlots dragging={false} placements={{}} interactive={false} />}
                    </div>
                  )}
                </section>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  )
}
