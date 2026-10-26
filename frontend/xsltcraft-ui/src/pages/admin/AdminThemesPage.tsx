import { Fragment, useEffect, useState } from 'react'
import { FileCode2, Loader2, Pencil, ScanSearch, Trash2 } from 'lucide-react'
import {
  getAdminThemes,
  getAdminTheme,
  deleteTheme,
  type AdminTheme,
} from '../../services/adminService'
import type { FreeTheme } from '../../services/templateService'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import IconTile from '../../components/ui/IconTile'
import ThemeSlotReport from '../../components/admin/ThemeSlotReport'
import { UpdateRow, UploadForm } from '../../components/admin/themes/ThemeForms'
import {
  AdminPage, Banner, BTN_DANGER, BTN_ICON, BTN_ICON_DANGER, BTN_SECONDARY, Modal, Pill, SectionHeader, TABLE, TD, TH, TR,
} from '../../components/admin/adminUi'

const DOC_TYPE_LABEL: Record<string, string> = {
  Invoice: 'e-Fatura',
  Despatch: 'İrsaliye',
}

export default function AdminThemesPage() {
  const [themes, setThemes] = useState<FreeTheme[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [deletingTheme, setDeletingTheme] = useState<FreeTheme | null>(null)
  const [deleteLoading, setDeleteLoading] = useState(false)
  const [report, setReport] = useState<AdminTheme | null>(null)

  useEffect(() => {
    getAdminThemes()
      .then(setThemes)
      .catch(() => setError('Temalar yüklenemedi.'))
      .finally(() => setLoading(false))
  }, [])

  // Yükleme/güncelleme sonrası konum analizi raporu gösterilir.
  const handleCreated = (t: AdminTheme) => {
    setThemes((prev) => [t, ...prev])
    setReport(t)
  }

  const handleUpdated = (t: AdminTheme) => {
    setThemes((prev) => prev.map((x) => (x.id === t.id ? t : x)))
    if (t.slotAnalysis) setReport(t)
  }

  const openReport = async (id: string) => {
    try {
      setReport(await getAdminTheme(id))
    } catch {
      setError('Tema bilgisi alınamadı.')
    }
  }

  const handleDeleteConfirm = async () => {
    if (!deletingTheme) return
    setDeleteLoading(true)
    try {
      await deleteTheme(deletingTheme.id)
      setThemes((prev) => prev.filter((x) => x.id !== deletingTheme.id))
      setDeletingTheme(null)
    } catch {
      // silme hatası — modal açık kalır
    } finally {
      setDeleteLoading(false)
    }
  }

  return (
    <AdminPage>
      <PageHeader title="Hazır Şablonlar" description="Hazır XSLT temalarını yönetin (ücretsiz / ücretli)." />

      <div className="flex flex-wrap items-start gap-4">
        <UploadForm onCreated={handleCreated} />

        <Card as="section" padding="none" aria-labelledby="themes-list-title" className="min-w-0 flex-[999_1_520px]">
          <SectionHeader
            id="themes-list-title"
            title="Mevcut Temalar"
            aside={!loading && <span className="text-[12.5px] text-tx-muted">{themes.length} tema</span>}
          />

          {error && <div className="px-4 pt-3"><Banner>{error}</Banner></div>}

          {loading && (
            <div className="flex items-center justify-center gap-2 py-12 text-[13px] text-tx-muted">
              <Loader2 size={15} className="animate-spin" aria-hidden="true" /> Yükleniyor…
            </div>
          )}

          {!loading && !error && themes.length === 0 && (
            <p className="m-0 px-4 py-10 text-center text-[13px] text-tx-muted">Henüz tema yok.</p>
          )}

          {!loading && themes.length > 0 && (
            <div className="overflow-x-auto">
              <table className={TABLE}>
                <thead>
                  <tr>
                    <th scope="col" className={`${TH} px-4!`}>Tema Adı</th>
                    <th scope="col" className={TH}>Tip</th>
                    <th scope="col" className={TH}>Ücret</th>
                    <th scope="col" className={TH}>Yükleme Tarihi</th>
                    <th scope="col" className={`${TH} px-4! text-right!`}>İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {themes.map((theme) => {
                    const editing = editingId === theme.id
                    return (
                      <Fragment key={theme.id}>
                        <tr className={`${TR} group ${editing ? 'bg-ui-soft' : ''}`}>
                          <td className={`${TD} px-4!`}>
                            <span className="flex items-center gap-2.5">
                              <IconTile icon={FileCode2} color="green" size="sm" />
                              <span className="font-semibold text-tx">{theme.name}</span>
                            </span>
                          </td>
                          <td className={`${TD} text-tx-2`}>{DOC_TYPE_LABEL[theme.documentType] ?? theme.documentType}</td>
                          <td className={TD}>
                            {theme.isPremium ? <Pill tone="purple">Ücretli</Pill> : <Pill tone="green">Ücretsiz</Pill>}
                          </td>
                          <td className={`${TD} text-tx-muted`}>{new Date(theme.createdAt).toLocaleDateString('tr-TR')}</td>
                          <td className={`${TD} px-4!`}>
                            <span className="flex justify-end gap-0.5">
                              <button
                                type="button"
                                onClick={() => void openReport(theme.id)}
                                aria-label={`${theme.name}: logo / imza konum analizi`}
                                title="Logo / imza konum analizi"
                                className={BTN_ICON}
                              >
                                <ScanSearch size={15} aria-hidden="true" />
                              </button>
                              <button
                                type="button"
                                onClick={() => setEditingId(editing ? null : theme.id)}
                                aria-label={`${theme.name}: güncelle`}
                                aria-expanded={editing}
                                title="Güncelle"
                                className={BTN_ICON}
                              >
                                <Pencil size={15} aria-hidden="true" />
                              </button>
                              <button
                                type="button"
                                onClick={() => setDeletingTheme(theme)}
                                aria-label={`${theme.name}: sil`}
                                title="Temayı sil"
                                className={BTN_ICON_DANGER}
                              >
                                <Trash2 size={15} aria-hidden="true" />
                              </button>
                            </span>
                          </td>
                        </tr>
                        {editing && (
                          <tr className="bg-ui-soft">
                            <td colSpan={5} className="px-4 pt-1 pb-4">
                              <UpdateRow
                                theme={theme}
                                onCancel={() => setEditingId(null)}
                                onUpdated={(t) => {
                                  handleUpdated(t)
                                  setEditingId(null)
                                }}
                              />
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </div>

      {report && <ThemeSlotReport theme={report} onClose={() => setReport(null)} />}

      {deletingTheme && (
        <Modal
          titleId="delete-theme-title"
          title="Temayı Sil"
          onClose={() => setDeletingTheme(null)}
          footer={
            <>
              <button type="button" onClick={() => setDeletingTheme(null)} className={`${BTN_SECONDARY} h-10! px-4!`}>İptal</button>
              <button type="button" onClick={() => void handleDeleteConfirm()} disabled={deleteLoading} className={BTN_DANGER}>
                {deleteLoading ? 'Siliniyor…' : 'Evet, Sil'}
              </button>
            </>
          }
        >
          <p className="m-0 text-[13px] text-tx-2">
            <span className="font-semibold text-tx">{deletingTheme.name}</span> teması kalıcı olarak silinecek. Bu işlem geri alınamaz.
          </p>
        </Modal>
      )}
    </AdminPage>
  )
}
