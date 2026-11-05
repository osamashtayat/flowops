/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on HTTPS (cookie is __Host-, Secure). The dev proxy keeps the SPA same-origin,
// so no CORS is needed. Override the target with FLOWOPS_API_URL if your port differs.
const apiTarget = process.env.FLOWOPS_API_URL ?? 'https://localhost:7180'

export default defineConfig(({ mode }) => ({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    // Tests read two repo-level contract files (contracts/openapi.json and the backend's
    // ErrorCodes.cs) to catch drift; the dev server keeps Vite's default of serving only this app.
    fs: mode === 'test' ? { allow: ['../..'] } : {},
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: false, secure: false },
      '/hubs': { target: apiTarget, changeOrigin: false, secure: false, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
}))
