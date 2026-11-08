import EasyEditWorkspace from '../components/theme-use/EasyEditWorkspace'
import { useDocEasyEditSource } from '../components/easy-edit/docEasyEditSource'
import { useEasyEditDoc } from '../components/easy-edit/easyEditDoc'

/** Yüklenen XSLT ya da kayıtlı şablon için Kolay düzenle; belge EasyEditScope'tan gelir. */
export default function UploadEasyEditPage() {
  const doc = useEasyEditDoc()
  const source = useDocEasyEditSource(doc)
  return <EasyEditWorkspace source={source} />
}
