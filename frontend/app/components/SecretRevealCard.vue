<template>
  <div class="space-y-4">
    <p class="text-sm font-medium text-gray-700">
      Secret (viewed once — it’s now gone):
    </p>
    <div
      class="p-4 rounded-input bg-gray-100 border border-gray-200 font-mono text-sm text-gray-900 whitespace-pre-wrap break-words select-text"
      role="region"
      aria-label="Secret content"
    >
      {{ secret }}
    </div>
    <p class="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-input px-3 py-2">
      This secret will expire after this view. Do not refresh if you need to copy it.
    </p>
    <div class="flex flex-wrap gap-3">
      <button
        type="button"
        class="inline-flex items-center gap-2 px-4 py-2.5 rounded-input font-medium text-white bg-primary-600 hover:bg-primary-700 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 transition-colors"
        :aria-label="copyLabel"
        @click="copySecret"
      >
        <span v-if="copied" aria-hidden="true">✓</span>
        <span>{{ copyButtonText }}</span>
      </button>
      <NuxtLink
        to="/"
        class="inline-flex items-center px-4 py-2.5 rounded-input font-medium text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 transition-colors"
      >
        Create new secret
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{ secret: string }>()

const copied = ref(false)
const copyLabel = computed(() => (copied.value ? 'Secret copied to clipboard' : 'Copy secret'))
const copyButtonText = computed(() => (copied.value ? 'Copied!' : 'Copy secret'))

async function copySecret () {
  try {
    await navigator.clipboard.writeText(props.secret)
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  } catch {
    // Fallback: user can select and copy manually
  }
}
</script>
