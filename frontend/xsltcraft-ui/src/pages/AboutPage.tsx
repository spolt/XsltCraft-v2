import { FileCode2, BookOpen, Sparkles, FolderOpen } from 'lucide-react'

type Feature = {
  icon: React.ComponentType<{ size?: number; className?: string }>
  title: string
  description: string
  color: string
}

const features: Feature[] = [
  {
    icon: FileCode2,
    title: 'XSLT Editör',
    description: 'Monaco tabanlı editör, canlı önizleme ve hata paneli.',
    color: 'text-purple-500',
  },
  {
    icon: BookOpen,
    title: 'Tema Kütüphanesi',
    description: 'Hazır UBL-TR şablonları (Invoice, CreditNote, DespatchAdvice).',
    color: 'text-green-500',
  },
  {
    icon: Sparkles,
    title: 'AI Asistan',
    description: 'Türkçe doğal dilde XSLT refactor ve sohbet (Ollama + Gemini fallback).',
    color: 'text-blue-500',
  },
  {
    icon: FolderOpen,
    title: 'Taslaklar & Şablonlarım',
    description: 'Kişisel kayıt, düzenleme ve sürüm takibi.',
    color: 'text-gray-500',
  },
]

export default function AboutPage() {
  return (
    <div className="max-w-3xl mx-auto px-6 py-10">
      <div className="flex items-center gap-3 mb-1">
        <h1 className="text-2xl font-semibold text-gray-800">XsltCraft</h1>
        <span className="text-xs font-medium text-blue-700 bg-blue-50 border border-blue-100 rounded-full px-2 py-0.5">
          v{__APP_VERSION__}
        </span>
      </div>
      <p className="text-gray-500 mb-8 text-sm">
        UBL-TR e-Fatura ve e-Arşiv XSLT şablonları için görsel editör.
      </p>

      <div className="bg-white border border-gray-200 rounded-xl p-6 mb-4">
        <h2 className="text-sm font-semibold text-gray-700 mb-3">Uygulama Hakkında</h2>
        <p className="text-sm text-gray-600 leading-relaxed">
          XsltCraft, UBL-TR e-Fatura ve e-Arşiv XSLT şablonlarını kolayca oluşturmak ve düzenlemek
          için tasarlanmış bir görsel editördür. Hazır tema kütüphanesi, kişisel taslak yönetimi ve AI destekli refactor
          ile şablon hazırlamayı hızlandırır.
        </p>
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-6 mb-8">
        <h2 className="text-sm font-semibold text-gray-700 mb-4">Öne Çıkan Özellikler</h2>
        <ul className="flex flex-col gap-4">
          {features.map(({ icon: Icon, title, description, color }) => (
            <li key={title} className="flex items-start gap-3">
              <Icon size={20} className={`${color} flex-shrink-0 mt-0.5`} />
              <div>
                <p className="text-sm font-medium text-gray-800">{title}</p>
                <p className="text-xs text-gray-500 mt-0.5">{description}</p>
              </div>
            </li>
          ))}
        </ul>
      </div>

      <p className="text-xs text-gray-400 text-center">© 2026 XsltCraft</p>
    </div>
  )
}
