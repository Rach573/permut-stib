<script setup lang="ts">
defineProps<{ name: 'home' | 'permutation' | 'signature' | 'bell' | 'logout' | 'search' | 'list' | 'help' | 'calendar' | 'users' | 'audit' }>()
</script>

<template>
  <svg class="app-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <template v-if="name === 'home'">
      <path d="M3.5 10.5 12 3.7l8.5 6.8" />
      <path d="M5.5 9.5v10.2h13V9.5M9.5 19.7v-6h5v6" />
    </template>
    <template v-else-if="name === 'permutation'">
      <rect x="3" y="4.5" width="18" height="16" rx="3" />
      <path d="M7 2.5v4M17 2.5v4M3 8.5h18M7 12h9l-2.2-2.2M17 16H8l2.2 2.2" />
    </template>
    <template v-else-if="name === 'signature'">
      <path d="M5 3.5h9l5 5v12H5zM14 3.5v5h5" />
      <path d="m8 16.5 1.1-3.1 6.8-6.8 2 2-6.8 6.8zM8 18.5h8" />
    </template>
    <template v-else-if="name === 'bell'">
      <path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 8.5h18C21 16 18 16 18 9ZM9.5 21h5" />
    </template>
    <template v-else-if="name === 'logout'">
      <path d="M10 4H5v16h5M14 8l4 4-4 4M18 12H9" />
    </template>
    <template v-else-if="name === 'search'">
      <circle cx="10.5" cy="10.5" r="6.5" /><path d="m15.5 15.5 4.5 4.5" />
    </template>
    <template v-else-if="name === 'list'">
      <path d="M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01" />
    </template>
    <template v-else-if="name === 'help'">
      <path d="M4 13.5c2.2-1.9 4-2.8 5.5-2.8 2.1 0 2.7 1.4 4.4 1.4 1.6 0 2.8-1 6.1-3.1" />
      <path d="M4 17.5c2.2-1.9 4-2.8 5.5-2.8 2.1 0 2.7 1.4 4.4 1.4 1.6 0 2.8-1 6.1-3.1M6 8.5 9 5l3 3.5" />
    </template>
    <template v-else-if="name === 'users'">
      <circle cx="9" cy="8" r="3" /><path d="M3.5 20v-2.2A4.8 4.8 0 0 1 8.3 13h1.4a4.8 4.8 0 0 1 4.8 4.8V20M15 5.5a3 3 0 0 1 0 5.5M17 13.5a4.5 4.5 0 0 1 3.5 4.3V20" />
    </template>
    <template v-else-if="name === 'audit'">
      <path d="M6 3.5h12v17H6zM9 8h6M9 12h6M9 16h4M4 6H2.5M4 11H2.5M4 16H2.5" />
    </template>
    <template v-else>
      <rect x="3" y="5" width="18" height="16" rx="3" />
      <path d="M7 3v4M17 3v4M3 9h18M12 12v6M9 15h6" />
    </template>
  </svg>
</template>
