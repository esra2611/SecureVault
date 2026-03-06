<template>
  <div
    class="text-center py-8 px-4"
    role="status"
    aria-live="polite"
    aria-label="Secret expired"
  >
    <div
      class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-amber-100 text-amber-600 mb-4"
      aria-hidden="true"
    >
      <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
      </svg>
    </div>
    <h2 class="text-xl font-semibold text-gray-900 mb-2">
      This secret has expired or has already been viewed.
    </h2>
    <p class="text-gray-600 text-sm max-w-sm mx-auto mb-6">
      One-time links cannot be opened again. Create a new link if you need to share another secret.
    </p>
    <NuxtLink
      to="/"
      class="inline-flex items-center px-5 py-2.5 rounded-input font-medium text-white bg-primary-600 hover:bg-primary-700 focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 transition-colors"
    >
      Create new secret
    </NuxtLink>
  </div>
</template>

<script setup lang="ts">
</script>
