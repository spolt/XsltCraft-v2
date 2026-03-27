import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import LoginPage from './pages/auth/LoginPage'
import RegisterPage from './pages/auth/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import TemplatesPage from './pages/TemplatesPage'
import DraftsPage from './pages/DraftsPage'
import ProfilePage from './pages/ProfilePage'
import AdminThemesPage from './pages/admin/AdminThemesPage'
import EditorPage from './pages/EditorPage'
import ThemeUsePage from './pages/ThemeUsePage'
import DevModePage from './pages/DevModePage'
import XsltEditorPage from './pages/XsltEditorPage'
import MyXsltTemplatesPage from './pages/MyXsltTemplatesPage'
import PrivateRoute from './components/PrivateRoute'
import AdminRoute from './components/AdminRoute'
import AppLayout from './components/layout/AppLayout'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/auth/login" element={<LoginPage />} />
        <Route path="/auth/register" element={<RegisterPage />} />

        {/* AppLayout kullanan sayfalar */}
        <Route element={<PrivateRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/templates" element={<TemplatesPage />} />
            <Route path="/drafts" element={<DraftsPage />} />
            <Route path="/my-xslt-templates" element={<MyXsltTemplatesPage />} />
            <Route path="/profile" element={<ProfilePage />} />
          </Route>
          {/* Editor ve ThemeUse kendi layout'larını yönetir */}
          <Route path="/editor/new" element={<EditorPage />} />
          <Route path="/editor/:templateId" element={<EditorPage />} />
          <Route path="/theme-use/:templateId" element={<ThemeUsePage />} />
          <Route path="/dev-mode/:templateId" element={<DevModePage />} />
          <Route path="/xslt-editor" element={<XsltEditorPage />} />
          <Route path="/xslt-editor/:templateId" element={<XsltEditorPage />} />
        </Route>

        <Route element={<AdminRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/admin" element={<Navigate to="/admin/themes" replace />} />
            <Route path="/admin/themes" element={<AdminThemesPage />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/auth/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
