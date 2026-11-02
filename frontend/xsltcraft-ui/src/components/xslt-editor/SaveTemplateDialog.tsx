import { useState } from 'react'
import { X } from 'lucide-react'

interface Props {
  defaultName: string
  isUpdate: boolean
  saving: boolean
  onSave: (name: string) => void
  onClose: () => void
}

export default function SaveTemplateDialog({ defaultName, isUpdate, saving, onSave, onClose }: Props) {
  const [name, setName] = useState(defaultName)

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    onSave(name.trim())
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div className="bg-ui-card rounded-xl shadow-2xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-tx">
            {isUpdate ? 'Şablonu Güncelle' : 'Şablonu Kaydet'}
          </h2>
          <button onClick={onClose} className="text-tx-subtle hover:text-tx-2 transition-colors">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <label className="block text-sm text-tx-2 mb-1">Şablon Adı</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Örn: e-Fatura Şablonum"
            autoFocus
            className="w-full border border-field-line rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ui-accent focus:border-ui-accent"
          />

          <div className="flex justify-end gap-2 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm text-tx-2 hover:text-tx hover:bg-ui-hover rounded-lg transition-colors"
            >
              İptal
            </button>
            <button
              type="submit"
              disabled={!name.trim() || saving}
              className="px-4 py-2 text-sm font-medium text-ui-accent-fg bg-ui-accent hover:brightness-110 rounded-lg disabled:opacity-50 transition-colors"
            >
              {saving ? 'Kaydediliyor…' : isUpdate ? 'Güncelle' : 'Kaydet'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
