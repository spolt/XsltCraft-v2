import { useParams } from 'react-router-dom'
import EasyEditWorkspace from '../components/theme-use/EasyEditWorkspace'
import { useThemeEasyEditSource } from '../components/theme-use/easyEditSource'

/** Tema Kullan: hazır temayı Kolay düzenle çalışma alanında açar. */
export default function ThemeUsePage() {
  const { templateId } = useParams<{ templateId: string }>()
  const source = useThemeEasyEditSource(templateId)
  return <EasyEditWorkspace source={source} />
}
