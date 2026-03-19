import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import LoginPage from './pages/auth/LoginPage'
import RegisterPage from './pages/auth/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import TemplatesPage from './pages/TemplatesPage'
import AdminThemesPage from './pages/admin/AdminThemesPage'
import EditorPage from './pages/EditorPage'
import PrivateRoute from './components/PrivateRoute'
import AdminRoute from './components/AdminRoute'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/auth/login" element={<LoginPage />} />
        <Route path="/auth/register" element={<RegisterPage />} />
        <Route element={<PrivateRoute />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/templates" element={<TemplatesPage />} />
          <Route path="/editor/new" element={<EditorPage />} />
          <Route path="/editor/:templateId" element={<EditorPage />} />
        </Route>
        <Route element={<AdminRoute />}>
          <Route path="/admin" element={<Navigate to="/admin/themes" replace />} />
          <Route path="/admin/themes" element={<AdminThemesPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/auth/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
