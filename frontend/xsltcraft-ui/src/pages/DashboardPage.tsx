import { Link } from 'react-router-dom'
import { FilePlus, BookOpen, FolderOpen, Code2, FileCode2 } from 'lucide-react'
import { useAuthStore } from '../store/authStore'

export default function DashboardPage() {
  const { user } = useAuthStore()

  return (
    <div className="max-w-3xl mx-auto px-6 py-10">
      <h1 className="text-2xl font-semibold text-gray-800 mb-1">
        Hoş geldin{user?.displayName ? `, ${user.displayName}` : ''} 👋
      </h1>
      <p className="text-gray-500 mb-8 text-sm">XsltCraft ile e-belge şablonlarını kolayca oluştur.</p>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-4">
        <Link
          to="/editor/new"
          className="flex flex-col items-center gap-3 p-6 bg-white rounded-xl border border-gray-200 hover:border-blue-400 hover:shadow-sm transition-all group"
        >
          <FilePlus size={32} className="text-blue-500 group-hover:scale-110 transition-transform" />
          <span className="text-sm font-medium text-gray-700">Yeni Şablon Oluştur</span>
        </Link>

        <Link
          to="/templates?type=Invoice"
          className="flex flex-col items-center gap-3 p-6 bg-white rounded-xl border border-gray-200 hover:border-blue-400 hover:shadow-sm transition-all group"
        >
          <BookOpen size={32} className="text-green-500 group-hover:scale-110 transition-transform" />
          <span className="text-sm font-medium text-gray-700">Tema Kütüphanesi</span>
        </Link>

        <Link
          to="/drafts"
          className="flex flex-col items-center gap-3 p-6 bg-white rounded-xl border border-gray-200 hover:border-gray-300 hover:shadow-sm transition-all group"
        >
          <FolderOpen size={32} className="text-gray-400 group-hover:scale-110 transition-transform" />
          <span className="text-sm font-medium text-gray-700">Taslaklarım</span>
        </Link>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Link
          to="/xslt-editor"
          className="flex flex-col items-center gap-3 p-6 bg-white rounded-xl border border-gray-200 hover:border-purple-400 hover:shadow-sm transition-all group"
        >
          <Code2 size={32} className="text-purple-500 group-hover:scale-110 transition-transform" />
          <span className="text-sm font-medium text-gray-700">XSLT Editör</span>
        </Link>

        <Link
          to="/my-xslt-templates"
          className="flex flex-col items-center gap-3 p-6 bg-white rounded-xl border border-gray-200 hover:border-purple-400 hover:shadow-sm transition-all group"
        >
          <FileCode2 size={32} className="text-purple-400 group-hover:scale-110 transition-transform" />
          <span className="text-sm font-medium text-gray-700">Şablonlarım</span>
        </Link>
      </div>
    </div>
  )
}
