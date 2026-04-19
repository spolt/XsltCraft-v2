import { useEffect } from 'react'
import { X, Keyboard } from 'lucide-react'

interface ShortcutRow {
  keys: string[]
  description: string
}

interface ShortcutGroup {
  title: string
  rows: ShortcutRow[]
}

const IS_MAC = typeof navigator !== 'undefined' && /Mac|iPhone|iPad|iPod/.test(navigator.platform)
const ctrl = IS_MAC ? '⌘' : 'Ctrl'
const alt  = IS_MAC ? '⌥' : 'Alt'
const shift = IS_MAC ? '⇧' : 'Shift'

const GROUPS: ShortcutGroup[] = [
  {
    title: 'Genel',
    rows: [
      { keys: [`${ctrl}+S`],               description: 'Kaydet' },
      { keys: ['F1'],                       description: 'Kısayollar (Bu Dialog)' },
    ],
  },
  {
    title: 'XSLT Editörü',
    rows: [
      { keys: [`${ctrl}+Z`],               description: 'Geri Al' },
      { keys: [`${ctrl}+Y`, `${ctrl}+${shift}+Z`], description: 'Yeniden Yap' },
      { keys: [`${ctrl}+F`],               description: 'Ara' },
      { keys: [`${ctrl}+H`],               description: 'Bul & Değiştir' },
      { keys: [`${ctrl}+G`],               description: 'Satıra Git' },
      { keys: [`${ctrl}+Space`],           description: 'Otomatik Tamamla' },
      { keys: [`${shift}+${alt}+F`],       description: 'Belgeyi Biçimlendir' },
      { keys: [`${ctrl}+${shift}+C`],      description: 'Açıklama Ekle / Kaldır' },
      { keys: [`${ctrl}+/`],               description: 'Satır Yorumu Ekle / Kaldır' },
      { keys: [`${ctrl}+D`],               description: 'Sonraki Eşleşmeyi Seç' },
      { keys: [`${ctrl}+${shift}+K`],      description: 'Satırı Sil' },
      { keys: [`${alt}+↑`, `${alt}+↓`],   description: 'Satırı Yukarı / Aşağı Taşı' },
      { keys: [`${shift}+${alt}+↑`, `${shift}+${alt}+↓`], description: 'Satırı Kopyala' },
      { keys: ['F12'],                     description: 'Tanıma Git' },
    ],
  },
  {
    title: 'Araç Çubuğu & Paneller',
    rows: [
      { keys: ['Ctrl+Shift+P (xslt)'],     description: 'Sorunlar panelini aç / kapat' },
      { keys: ['XPath butonu'],            description: 'XPath Konsolu aç / kapat' },
      { keys: ['Snippet butonu'],          description: 'Snippet Kütüphanesi aç' },
      { keys: ['UBL-TR butonu'],           description: 'UBL-TR iş kurallarını doğrula' },
    ],
  },
  {
    title: 'Önizleme',
    rows: [
      { keys: ['+'],                       description: 'Yakınlaştır' },
      { keys: ['−'],                       description: 'Uzaklaştır' },
      { keys: ['%100'],                    description: 'Varsayılan Büyüklüğe Sıfırla' },
      { keys: ['Önizleme tıklama'],        description: 'Editörde ilgili satıra git' },
    ],
  },
]

function Key({ label }: { label: string }) {
  return (
    <kbd className="inline-flex items-center justify-center min-w-[28px] h-6 px-1.5 rounded border border-gray-600 bg-gray-700 text-gray-200 text-[11px] font-mono shadow-sm">
      {label}
    </kbd>
  )
}

interface Props {
  onClose: () => void
}

export default function ShortcutsDialog({ onClose }: Props) {
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      onClick={(e) => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="bg-gray-900 border border-gray-700 rounded-xl shadow-2xl w-full max-w-lg max-h-[85vh] flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-700 flex-shrink-0">
          <div className="flex items-center gap-2">
            <Keyboard size={17} className="text-gray-400" />
            <span className="text-sm font-semibold text-gray-200">Klavye Kısayolları</span>
          </div>
          <button
            onClick={onClose}
            className="text-gray-500 hover:text-gray-200 transition-colors"
            title="Kapat (Esc)"
          >
            <X size={17} />
          </button>
        </div>

        {/* Body */}
        <div className="overflow-y-auto flex-1 px-5 py-4 space-y-5">
          {GROUPS.map((group) => (
            <section key={group.title}>
              <h3 className="text-[10px] uppercase tracking-widest text-gray-500 font-semibold mb-2">
                {group.title}
              </h3>
              <div className="space-y-1">
                {group.rows.map((row) => (
                  <div key={row.description} className="flex items-center justify-between gap-4 py-1">
                    <span className="text-sm text-gray-300">{row.description}</span>
                    <div className="flex items-center gap-1 flex-shrink-0">
                      {row.keys.map((k, i) => (
                        <span key={k} className="flex items-center gap-1">
                          {i > 0 && <span className="text-gray-600 text-xs">/</span>}
                          {k.includes('+') || k.length <= 4
                            ? k.split('+').map((part, pi) => (
                                <span key={pi} className="flex items-center gap-0.5">
                                  {pi > 0 && <span className="text-gray-600 text-[10px]">+</span>}
                                  <Key label={part} />
                                </span>
                              ))
                            : <span className="text-xs text-gray-400 italic">{k}</span>
                          }
                        </span>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>

        {/* Footer */}
        <div className="px-5 py-3 border-t border-gray-700 flex-shrink-0">
          <p className="text-[11px] text-gray-600 text-center">
            Monaco Editor'ün tüm varsayılan kısayolları da geçerlidir.
          </p>
        </div>
      </div>
    </div>
  )
}
