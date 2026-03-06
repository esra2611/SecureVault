<template>
  <div class="space-y-4">
    <p class="text-sm font-medium text-gray-700">
      Your secret link (share once — it can only be viewed once):
    </p>
    <div
      class="flex flex-wrap items-center gap-2 p-4 rounded-input bg-gray-50 border border-gray-200 break-all font-mono text-sm text-gray-800"
      role="status"
      aria-live="polite"
    >
      <a
        :href="shareUrl"
        class="text-primary-600 hover:underline focus:outline-none focus:ring-2 focus:ring-primary-500 rounded"
        target="_blank"
        rel="noopener noreferrer"
      >
        {{ shareUrl }}
      </a>
    </div>
    <p class="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-input px-3 py-2">
      Anyone with this link can view the secret once.
    </p>
    <div class="flex flex-wrap gap-3 pt-2">
      <button
        type="button"
        class="inline-flex items-center gap-2 px-4 py-2.5 rounded-input font-medium text-white bg-primary-600 hover:bg-primary-700 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 transition-colors"
        :aria-label="copyLabel"
        @click="copyLink"
      >
        <span v-if="copied" aria-hidden="true">✓</span>
        <span>{{ copyButtonText }}</span>
      </button>
      <button
        type="button"
        class="inline-flex items-center px-4 py-2.5 rounded-input font-medium text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 transition-colors"
        @click="$emit('reset')"
      >
        Share another
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{ shareUrl: string }>()
defineEmits<{ reset: [] }>()

const copied = ref(false)
const copyLabel = computed(() => (copied.value ? 'Link copied to clipboard' : 'Copy link to clipboard'))
const copyButtonText = computed(() => (copied.value ? 'Copied!' : 'Copy link'))

async function copyLink () {
  try {
    await navigator.clipboard.writeText(props.shareUrl)
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  } catch {
    // Fallback: open in new tab; do not log shareUrl
  }
}
</script>
