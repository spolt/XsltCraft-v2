import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { GoogleOAuthProvider } from '@react-oauth/google'
import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/ibm-plex-sans/700.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import './index.css'
import App from './App.tsx'
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
