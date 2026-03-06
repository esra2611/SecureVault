<template>
  <form
    class="space-y-5"
    novalidate
    @submit.prevent="onSubmit"
    aria-label="Create secret link"
  >
    <div class="space-y-2">
      <label for="secret-input" class="block text-sm font-medium text-gray-700">
        Your secret
      </label>
      <textarea
        id="secret-input"
        v-model="localPlaintext"
        class="w-full min-h-[140px] sm:min-h-[160px] px-4 py-3 rounded-input border border-gray-300 bg-white text-gray-900 placeholder-gray-500 focus:border-primary-500 focus:ring-2 focus:ring-primary-500/20 focus:outline-none resize-y transition-shadow"
        placeholder="Enter your secret (max 1000 characters)"
        rows="5"
        maxlength="1000"
        :aria-invalid="!!errors.plaintext"
        :aria-describedby="describedByPlaintext"
        :disabled="loading"
        @blur="trimBlur"
      />
      <p
        id="secret-counter"
        class="text-sm text-gray-500 tabular-nums"
        aria-live="polite"
      >
        {{ localPlaintext.length }} / 1000
      </p>
      <p
        v-if="errors.plaintext"
        id="secret-error"
        class="text-sm text-red-600"
        role="alert"
      >
        {{ errors.plaintext }}
      </p>
    </div>

    <div class="space-y-2">
      <label for="password-input" class="block text-sm font-medium text-gray-700">
        Optional password
      </label>
      <input
        id="password-input"
        v-model="localPassword"
        type="password"
        autocomplete="off"
        class="w-full px-4 py-3 rounded-input border border-gray-300 bg-white text-gray-900 placeholder-gray-500 focus:border-primary-500 focus:ring-2 focus:ring-primary-500/20 focus:outline-none"
        placeholder="Leave empty for no password"
        :disabled="loading"
        :aria-invalid="!!errors.password"
        :aria-describedby="errors.password ? 'password-error' : undefined"
      />
      <p
        v-if="errors.password"
        id="password-error"
        class="text-sm text-red-600"
        role="alert"
      >
        {{ errors.password }}
      </p>
      <p class="text-xs text-gray-500">
        If set, the recipient will need this password to view the secret.
      </p>
    </div>

    <div class="space-y-2">
      <label for="expiry-select" class="block text-sm font-medium text-gray-700">
        Expiry
      </label>
      <select
        id="expiry-select"
        v-model="localExpiry"
        class="w-full px-4 py-3 rounded-input border border-gray-300 bg-white text-gray-900 focus:border-primary-500 focus:ring-2 focus:ring-primary-500/20 focus:outline-none appearance-none bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20fill%3D%22none%22%20viewBox%3D%220%200%2020%2020%22%3E%3Cpath%20stroke%3D%22%236b7280%22%20stroke-linecap%3D%22round%22%20stroke-linejoin%3D%22round%22%20stroke-width%3D%221.5%22%20d%3D%22m6%208%204%204%204-4%22%2F%3E%3C%2Fsvg%3E')] bg-[length:1.5rem] bg-[right_0.5rem_center] bg-no-repeat pr-10"
        :aria-invalid="!!errors.expiry"
        :aria-describedby="describedByExpiry"
        :disabled="loading"
      >
        <option value="">Select expiry…</option>
        <option value="burn">Burn after reading</option>
        <option value="1h">1 hour</option>
        <option value="24h">24 hours</option>
        <option value="7d">7 days</option>
      </select>
      <p
        v-if="errors.expiry"
        id="expiry-error"
        class="text-sm text-red-600"
        role="alert"
      >
        {{ errors.expiry }}
      </p>
    </div>

    <div class="pt-1">
      <button
        type="submit"
        class="w-full sm:w-auto min-w-[140px] px-5 py-3 rounded-input font-medium text-white bg-primary-600 hover:bg-primary-700 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:bg-primary-600 transition-colors inline-flex items-center justify-center gap-2"
        :disabled="!canSubmit || loading"
        :aria-busy="loading"
      >
        <span v-if="loading" class="inline-block w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" aria-hidden="true" />
        <span>{{ loading ? 'Creating…' : 'Create link' }}</span>
      </button>
      <p
        v-if="errors.general"
        class="mt-3 text-sm text-red-600"
        role="alert"
      >
        {{ errors.general }}
      </p>
    </div>
  </form>
</template>

<script setup lang="ts">
const MAX_LENGTH = 1000

const props = withDefaults(
  defineProps<{
    plaintext: string
    expiry: string
    password: string
    errors: { plaintext?: string; expiry?: string; password?: string; general?: string }
    loading: boolean
  }>(),
  { plaintext: '', expiry: '', password: '', errors: () => ({}), loading: false }
)

const emit = defineEmits<{
  'update:plaintext': [v: string]
  'update:expiry': [v: string]
  'update:password': [v: string]
  submit: []
}>()

const localPlaintext = computed({
  get: () => props.plaintext,
  set: (v: string) => emit('update:plaintext', v)
})

const localExpiry = computed({
  get: () => props.expiry,
  set: (v: string) => emit('update:expiry', v)
})

const localPassword = computed({
  get: () => props.password,
  set: (v: string) => emit('update:password', v)
})

const trimmed = computed(() => props.plaintext.trim())

const canSubmit = computed(() => {
  if (!trimmed.value) return false
  if (trimmed.value.length > MAX_LENGTH) return false
  if (!props.expiry) return false
  return true
})

const describedByPlaintext = computed(() => {
  const ids = ['secret-counter']
  if (props.errors.plaintext) ids.push('secret-error')
  return ids.join(' ') || undefined
})

const describedByExpiry = computed(() => (props.errors.expiry ? 'expiry-error' : undefined))

function trimBlur () {
  const t = props.plaintext.trim()
  if (t !== props.plaintext) emit('update:plaintext', t)
}

function onSubmit () {
  emit('submit')
}
</script>
