<template>
  <div
    class="rounded-xl bg-white p-6 sm:p-8 shadow-sm border border-gray-100"
    :class="$attrs.class"
  >
    <slot />
  </div>
</template>

<script setup lang="ts">
defineOptions({ inheritAttrs: false })
</script>
