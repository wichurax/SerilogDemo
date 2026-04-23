import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'sonner'

import '@fontsource/outfit/400.css'
import '@fontsource/outfit/500.css'
import '@fontsource/outfit/600.css'
import '@fontsource/playfair-display/400.css'
import '@fontsource/playfair-display/600.css'
import '@fontsource/dm-mono/400.css'
import '@fontsource/dm-mono/500.css'

import { queryClient } from '@/lib/query-client'

import './index.css'
import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
      <Toaster
        richColors
        position='top-right'
        toastOptions={{
          className: 'border border-border bg-panel text-foreground shadow-md',
        }}
      />
    </QueryClientProvider>
  </StrictMode>,
)
