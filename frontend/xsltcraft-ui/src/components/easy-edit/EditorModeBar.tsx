import { useState } from 'react'
import type { EasyEditModeSwitch } from '../../hooks/useEasyEditModeSwitch'
import DocumentBar from './DocumentBar'
import EasyEditHintBand from './EasyEditHintBand'

interface Props {
  modeSwitch: EasyEditModeSwitch
  fileName: string
  dirty: boolean
  saving: boolean
  printDisabled: boolean
  onPrint: () => void
  onSave: () => void
  onDownload: () => void
}

/** XSLT Editör'ün belge çubuğu (Pro ve üstü): mod anahtarı, kaydet/indir ve Kolay düzenle öneri bandı. */
export default function EditorModeBar({ modeSwitch, fileName, dirty, saving, printDisabled, onPrint, onSave, onDownload }: Props) {
  // Süre dolunca kapanan band bu açılışta bir daha gösterilmez; öneri düşünce (başka dosya) sıfırlanır.
  const [autoHidden, setAutoHidden] = useState(false)
  if (autoHidden && !modeSwitch.showHint) setAutoHidden(false)

  if (!modeSwitch.showDocumentBar) return null
  const { switching, switchToEasyEdit } = modeSwitch
  return (
    <>
      <DocumentBar
        fileName={fileName}
        dirty={dirty}
        mode="editor"
        onModeChange={(mode) => { if (mode === 'easyEdit') void switchToEasyEdit() }}
        modeDisabled={saving || switching}
        onPrint={onPrint}
        onSave={onSave}
        onDownload={onDownload}
        printDisabled={printDisabled}
        saveDisabled={saving}
      />
      {modeSwitch.showHint && !autoHidden && (
        <EasyEditHintBand
          onSwitch={() => void switchToEasyEdit()}
          onDismiss={modeSwitch.dismissHint}
          onAutoHide={() => setAutoHidden(true)}
          busy={switching}
        />
      )}
    </>
  )
}
