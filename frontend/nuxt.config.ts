// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  modules: ['@nuxtjs/tailwindcss'],
  css: ['~/assets/css/main.css'],
  runtimeConfig: {
    // Server-side only: used when frontend container calls API (SSR). In Docker set NUXT_API_BASE=http://api:8080
    apiBaseInternal: process.env.NUXT_API_BASE || 'http://localhost:8080',
    public: {
      // Client (browser): must be URL reachable from browser. API runs on port 8080 (Docker and local).
      apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:8080'
    }
  }
})