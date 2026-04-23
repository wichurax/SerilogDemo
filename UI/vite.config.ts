import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'

function normalizeProxyTarget(target: string) {
  return target.replace(/\/+$/, '')
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_PROXY_API_TARGET || 'http://localhost:8080'
  const fulfillmentTarget = env.VITE_PROXY_FULFILLMENT_TARGET || apiTarget
  const shouldRewriteFulfillmentPrefix = normalizeProxyTarget(fulfillmentTarget) !== normalizeProxyTarget(apiTarget)
  const proxy = {
    '/api': {
      target: apiTarget,
      changeOrigin: true,
    },
    '/fulfillment-api': {
      target: fulfillmentTarget,
      changeOrigin: true,
      ...(shouldRewriteFulfillmentPrefix
        ? {
            rewrite: (path: string) => path.replace(/^\/fulfillment-api/, ''),
          }
        : {}),
    },
  }

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      proxy,
    },
    preview: {
      proxy,
    },
  }
})
