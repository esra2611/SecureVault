<template>
  <div class="min-h-screen py-8 px-4 sm:px-6 flex flex-col items-center">
    <div class="w-full max-w-lg mx-auto">
      <header class="text-center mb-8">
        <h1 class="text-2xl sm:text-3xl font-semibold text-gray-900 tracking-tight">
          SecureVault
        </h1>
      </header>

      <BaseCard>
        <div v-if="loading" class="py-12 flex flex-col items-center justify-center" role="status" aria-live="polite" aria-label="Loading secret">
          <span class="inline-block w-8 h-8 border-2 border-primary-600 border-t-transparent rounded-full animate-spin mb-3" aria-hidden="true" />
          <p class="text-sm text-gray-600">Loading…</p>
        </div>
        <SecretRevealCard v-else-if="plaintext !== null" :secret="plaintext" />
        <div v-else-if="errorMessage" class="py-8 text-center" role="alert">
          <p class="text-sm text-red-600">{{ errorMessage }}</p>
        </div>
        <div v-else-if="showPasswordPrompt" class="space-y-4">
          <p class="text-sm text-gray-700">
            This link may be password-protected. Enter the password to view the secret.
          </p>
          <form class="space-y-3" @submit.prevent="revealWithPassword">
            <label for="reveal-password" class="block text-sm font-medium text-gray-700 sr-only">Password</label>
            <input
              id="reveal-password"
              v-model="passwordAttempt"
              type="password"
              autocomplete="off"
              class="w-full px-4 py-3 rounded-input border border-gray-300 bg-white text-gray-900 placeholder-gray-500 focus:border-primary-500 focus:ring-2 focus:ring-primary-500/20 focus:outline-none"
              placeholder="Password"
              :disabled="loadingPassword"
              aria-label="Password"
            />
            <p v-if="passwordError" class="text-sm text-red-600" role="alert">
              {{ passwordError }}
            </p>
            <button
              type="submit"
              class="w-full sm:w-auto min-w-[120px] px-5 py-3 rounded-input font-medium text-white bg-primary-600 hover:bg-primary-700 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
              :disabled="loadingPassword || !passwordAttempt.trim()"
            >
              {{ loadingPassword ? 'Checking…' : 'Reveal secret' }}
            </button>
          </form>
          <p class="text-xs text-gray-500">
            If you don’t have a password, the secret may have expired or already been viewed.
          </p>
        </div>
        <ExpiredState v-else />
      </BaseCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useApiBase } from '../../../composables/useApiBase'

const route = useRoute()
const apiBase = useApiBase()
const token = computed(() => route.params.token as string)
const plaintext = ref<string | null>(null)
const loading = ref(true)
const showPasswordPrompt = ref(false)
const passwordAttempt = ref('')
const loadingPassword = ref(false)
const passwordError = ref('')
const errorMessage = ref('')

async function tryReveal (password?: string) {
  if (password !== undefined && password !== '') {
    // POST with password in body to avoid query-string encoding issues (e.g. + decoded as space)
    return await $fetch<{ plaintext: string }>(`${apiBase}/api/secrets/reveal`, {
      method: 'POST',
      body: { token: token.value, password }
    })
  }
  return await $fetch<{ plaintext: string }>(`${apiBase}/s/${token.value}`)
}

onMounted(async () => {
  if (typeof window !== 'undefined' && window.location.hash === '#p') {
    showPasswordPrompt.value = true
    loading.value = false
    return
  }
  try {
    const res = await tryReveal()
    plaintext.value = res.plaintext
  } catch (e: unknown) {
    plaintext.value = null
    const status = (e as { response?: { status?: number } })?.response?.status
    if (status === 401 || status === 403) {
      showPasswordPrompt.value = true
    } else if (status === 404 || status === 410 || status === 400) {
      // ExpiredState: showPasswordPrompt stays false
    } else {
      errorMessage.value = 'Something went wrong. Please try again later.'
    }
  } finally {
    loading.value = false
  }
})

async function revealWithPassword () {
  if (!passwordAttempt.value.trim()) return
  passwordError.value = ''
  loadingPassword.value = true
  try {
    const res = await tryReveal(passwordAttempt.value.trim())
    plaintext.value = res.plaintext
    showPasswordPrompt.value = false
  } catch (e: unknown) {
    const status = (e as { response?: { status?: number } })?.response?.status
    if (status === 401 || status === 403) {
      passwordError.value = 'Wrong password.'
    } else if (status === 404 || status === 410) {
      showPasswordPrompt.value = false
    } else {
      errorMessage.value = 'Temporary server error. Please try again later.'
      showPasswordPrompt.value = false
    }
  } finally {
    loadingPassword.value = false
  }
}
</script>
