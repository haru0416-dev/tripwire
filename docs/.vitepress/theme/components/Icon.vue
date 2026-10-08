<script setup lang="ts">
// A Lucide icon (lucide.dev, ISC) drawn inline, in the current text color unless a color is given.
import { computed } from "vue";
import { iconSvg } from "../catalog";

const props = defineProps<{ name: string; size?: number | string; color?: string }>();
const inner = computed(() => iconSvg(props.name));
</script>

<template>
  <svg class="tw-icon" :width="size ?? 16" :height="size ?? 16" viewBox="0 0 24 24" fill="none"
       :stroke="color ?? 'currentColor'" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
       aria-hidden="true" v-html="inner" />
</template>

<style scoped>
.tw-icon { display: inline-block; flex: none; vertical-align: -0.15em; }
</style>
