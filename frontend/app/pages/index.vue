<template>
  <div class="min-h-screen py-8 px-4 sm:px-6 flex flex-col items-center">
    <div class="w-full max-w-lg mx-auto">
      <header class="text-center mb-8">
        <h1 class="text-2xl sm:text-3xl font-semibold text-gray-900 tracking-tight">
          SecureVault
        </h1>
        <p class="mt-2 text-gray-600 text-sm sm:text-base max-w-md mx-auto">
          Share a secret with a time-limited link. View once, then it's gone.
        </p>
      </header>

      <BaseCard>
        <SecretForm
          v-if="!shareUrl"
          v-model:plaintext="plaintext"
          v-model:expiry="expiry"
          v-model:password="password"
          :errors="errors"
          :loading="loading"
          @submit="submit"
        />
        <SecretSuccessCard
          v-else
          :share-url="shareUrl"
          @reset="reset"
        />
      </BaseCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useApiBase } from '../../composables/useApiBase'

const MAX_LENGTH = 1000

const apiBase = useApiBase()
const plaintext = ref('')
const expiry = ref('')
const password = ref('')
const shareUrl = ref('')
const loading = ref(false)
const errors = ref<{ plaintext?: string; expiry?: string; password?: string; general?: string }>({})

const trimmedPlaintext = computed(() => plaintext.value.trim())

const canSubmit = computed(() => {
  if (!trimmedPlaintext.value) return false
  if (trimmedPlaintext.value.length > MAX_LENGTH) return false
  if (!expiry.value) return false
  return true
})

function validateClient (): boolean {
  const e: { plaintext?: string; expiry?: string; password?: string; general?: string } = {}
  const t = trimmedPlaintext.value
  if (!t) {
    e.plaintext = 'Secret cannot be empty'
  } else if (t.length > MAX_LENGTH) {
    e.plaintext = 'Secret must be at most 1000 characters'
  }
  if (!expiry.value) {
    e.expiry = 'Please select an expiry option'
  }
  if (password.value.length > 500) {
    e.password = 'Password must be at most 500 characters'
  }
  errors.value = e
  return Object.keys(e).length === 0
}

const ERROR_CODE_MESSAGES: Record<string, string> = {
  SECRET_EMPTY: 'Secret cannot be empty',
  SECRET_TOO_LONG: 'Secret must be at most 1000 characters',
  EXPIRY_REQUIRED: 'Please select an expiry option',
  EXPIRY_INVALID: 'Please select a valid expiry option'
}

function mapApiErrors (data: { errors?: Array<{ propertyName?: string; code?: string; message?: string }> }): void {
  const e: { plaintext?: string; expiry?: string; general?: string } = {}
  const list = data?.errors ?? []
  for (const err of list) {
    const msg = ERROR_CODE_MESSAGES[err.code ?? ''] ?? err.message ?? 'Validation failed'
    const prop = (err.propertyName ?? '').toLowerCase()
    if (prop === 'plaintext') e.plaintext = msg
    else if (prop === 'expiry') e.expiry = msg
    else e.general = e.general ? `${e.general}; ${msg}` : msg
  }
  if (list.length > 0 && !e.plaintext && !e.expiry) e.general = e.general ?? list.map(x => x.message ?? x.code).filter(Boolean).join('; ')
  errors.value = e
}

async function submit () {
  errors.value = {}
  if (!validateClient()) return
  loading.value = true
  try {
    const body: { plaintext: string; expiry: string; password?: string } = {
      plaintext: trimmedPlaintext.value,
      expiry: expiry.value
    }
    if (password.value.trim()) body.password = password.value.trim()
    const res = await $fetch<{ shareUrl: string }>(`${apiBase}/api/secrets`, {
      method: 'POST',
      body
    })
    shareUrl.value = res.shareUrl + (body.password ? '#p' : '')
  } catch (e: unknown) {
    const data = (e as { data?: unknown })?.data ?? (e as { response?: { _data?: unknown } })?.response?._data
    if (data && typeof data === 'object' && ((data as { errors?: unknown }).errors ?? (data as { status?: number }).status === 400)) {
      mapApiErrors(data as { errors?: Array<{ propertyName?: string; code?: string; message?: string }> })
    } else {
      errors.value = { general: 'Failed to create secret. Please try again.' }
    }
  } finally {
    loading.value = false
  }
}

function reset () {
  shareUrl.value = ''
  plaintext.value = ''
  expiry.value = ''
  password.value = ''
  errors.value = {}
}
</script>
