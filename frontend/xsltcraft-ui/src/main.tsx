import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { GoogleOAuthProvider } from '@react-oauth/google'
import './index.css'
import App from './App.tsx'
import 'flexlayout-react/style/dark.css'
import { useAiStore } from './store/aiStore'
import { ensureSession } from './services/authSession'

// AI feature flag durumunu app açılırken tek seferde sorgula.
useAiStore.getState().refresh()

// Access token bellekte tutulur; önceki oturum varsa refresh çereziyle sessizce geri yükle.
// PrivateRoute aynı isteği paylaşır (tek uçuş).
void ensureSession()

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <GoogleOAuthProvider clientId={googleClientId}>
      <App />
    </GoogleOAuthProvider>
  </StrictMode>,
)
