<script setup lang="ts">
// An "いつ → 何をする" card drawn like Tripwire's Inspector (Unity's dark or light skin, following the site theme).
// Names, icons and colors come from the editor's catalog, so the card says what the Inspector says.
//
//   <TriggerCard event="Interact" :actions="[{ id: 'GameObject.ToggleActive', rows: [{ label: { ja: '対象', en: 'Target' }, value: 'Mirror', kind: 'object' }] }]" />
import { computed } from "vue";
import Icon from "./Icon.vue";
import { useLang, type L } from "../lang";
import { eventById, actionById, eventIcon, actionIcon, categoryColor } from "../catalog";

/** A setting row. For an action, p picks the label from the catalog (its p-th setting) instead of label. */
type Row = { label?: L; p?: number; value?: L; kind?: "object" | "popup" | "text" | "toggle" | "number" };
type Action = { id: string; rows?: Row[]; then?: Action[]; else?: Action[]; note?: L };

const props = defineProps<{
  event: string;
  /** The name after the event: a timer's, a custom event's, a variable's. */
  name?: L;
  rows?: Row[];
  actions?: Action[];
  /** Hide the "+ add" button at the bottom. */
  noAdd?: boolean;
}>();

const { t } = useLang();
const spec = computed(() => eventById(props.event));
const color = computed(() => categoryColor(spec.value?.category));
const title = computed(() => (spec.value ? t(spec.value.name) : props.event) + (props.name ? "  —  " + t(props.name) : ""));

const actionTitle = (a: Action) => (actionById(a.id) ? t(actionById(a.id)!.name) : a.id);
const actionColor = (a: Action) => categoryColor(actionById(a.id)?.category);
const rowLabel = (a: Action | null, r: Row) => (a && r.p != null ? t(actionById(a.id)?.parameters[r.p]?.name) : t(r.label));
</script>

<template>
  <div class="tw-card" :style="{ '--cat': color }">
    <div class="head">
      <Icon name="grip-horizontal" class="grip" :size="12" />
      <Icon name="chevron-down" class="fold" :size="12" />
      <span class="tag">{{ t({ ja: "いつ", en: "When" }) }}</span>
      <Icon :name="eventIcon(event)" :color="color" class="cat-icon" />
      <span class="popup title">{{ title }}</span>
      <span class="more">⋯</span>
    </div>

    <div v-for="(r, i) in rows" :key="'r' + i" class="row">
      <span class="label">{{ t(r.label) }}</span>
      <span class="field" :class="r.kind ?? 'popup'">
        <Icon v-if="r.kind === 'object'" name="box" :size="12" class="obj" />
        <template v-if="r.kind === 'toggle'"><span class="check">{{ t(r.value) ? "✓" : "" }}</span></template>
        <template v-else>{{ t(r.value) }}</template>
      </span>
    </div>

    <div class="what">{{ t({ ja: "何をする", en: "What to do" }) }}</div>

    <template v-for="(a, ai) in actions" :key="'a' + ai">
      <div class="action" :style="{ '--acat': actionColor(a) }">
        <div class="head">
          <Icon name="grip-horizontal" class="grip" :size="12" />
          <Icon :name="actionIcon(a.id)" :color="actionColor(a)" class="cat-icon" />
          <span class="action-title">{{ actionTitle(a) }}</span>
          <span class="more">⋯</span>
        </div>
        <div v-for="(r, i) in a.rows" :key="'ar' + i" class="row">
          <span class="label">{{ rowLabel(a, r) }}</span>
          <span class="field" :class="r.kind ?? 'popup'">
            <Icon v-if="r.kind === 'object'" name="box" :size="12" class="obj" />
            <template v-if="r.kind === 'toggle'"><span class="check">{{ t(r.value) ? "✓" : "" }}</span></template>
            <template v-else>{{ t(r.value) }}</template>
          </span>
        </div>
        <template v-for="(branch, bi) in [a.then, a.else]" :key="'b' + bi">
          <template v-if="branch">
            <span class="block-tag">{{ bi === 1 ? "Else" : a.id === "Flow.If" ? "Then" : "Do" }}</span>
            <div v-for="(c, ci) in branch" :key="'c' + ci" class="action nested" :style="{ '--acat': actionColor(c) }">
              <div class="head">
                <Icon name="grip-horizontal" class="grip" :size="12" />
                <Icon :name="actionIcon(c.id)" :color="actionColor(c)" class="cat-icon" />
                <span class="action-title">{{ actionTitle(c) }}</span>
                <span class="more">⋯</span>
              </div>
              <div v-for="(r, i) in c.rows" :key="'cr' + i" class="row">
                <span class="label">{{ rowLabel(c, r) }}</span>
                <span class="field" :class="r.kind ?? 'popup'">
                  <Icon v-if="r.kind === 'object'" name="box" :size="12" class="obj" />
                  {{ t(r.value) }}
                </span>
              </div>
            </div>
          </template>
        </template>
      </div>
    </template>

    <div v-if="!noAdd" class="add">＋ {{ t({ ja: "何をするかを追加", en: "Add what to do" }) }}</div>
  </div>
</template>

<style scoped>
/* Unity's skins: light by default, dark with the site's dark theme. */
.tw-card {
  --ins-bg: #c8c8c8; --card: #d4d4d4; --card-line: #a8a8a8; --inner: #dedede;
  --field: #f0f0f0; --field-line: #9a9a9a; --popup: #e4e4e4; --text: #101010; --muted: #555; --tag-fg: #fff;
  margin: 20px 0; padding: 6px 8px 8px; border-radius: 4px;
  background: var(--card); border: 1px solid var(--card-line); box-shadow: inset 3px 0 0 var(--cat);
  color: var(--text); font: 12px/1.45 system-ui, -apple-system, "Segoe UI", "Hiragino Sans", "Noto Sans JP", sans-serif;
  max-width: 460px; user-select: none;
}
:global(.dark .tw-card) {
  --card: #3e3e3e; --card-line: #262626; --inner: #444; --field: #2a2a2a; --field-line: #1f1f1f;
  --popup: #515151; --text: #d2d2d2; --muted: #9a9a9a;
}
.head { display: flex; align-items: center; gap: 5px; min-height: 22px; }
.grip, .fold { color: var(--muted); }
.tag {
  padding: 0 5px; border-radius: 2px; background: color-mix(in srgb, var(--cat) 80%, black);
  color: var(--tag-fg); font-weight: 700; font-size: 11px; line-height: 16px;
}
.cat-icon { width: 16px; height: 16px; }
.popup {
  flex: 1; min-width: 0; padding: 1px 18px 1px 6px; border-radius: 3px; background: var(--popup);
  border: 1px solid var(--field-line); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; position: relative;
}
.popup::after { content: "▾"; position: absolute; right: 5px; color: var(--muted); }
.title { font-weight: 700; }
.more {
  padding: 0 6px; border-radius: 3px; background: var(--popup); border: 1px solid var(--field-line);
  color: var(--muted); line-height: 16px;
}
.row { display: flex; align-items: center; gap: 6px; min-height: 20px; margin-top: 2px; padding-left: 4px; }
.label { flex: 0 0 38%; color: var(--text); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.field {
  flex: 1; min-width: 0; padding: 1px 6px; border-radius: 3px; border: 1px solid var(--field-line);
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis; display: flex; align-items: center; gap: 4px;
  min-height: 18px; box-sizing: border-box;
}
.field.popup { background: var(--popup); position: relative; padding-right: 18px; }
.field.object, .field.text, .field.number { background: var(--field); }
.field.object::after { content: "◎"; margin-left: auto; color: var(--muted); font-size: 11px; }
.field.toggle { flex: 0 0 14px; height: 14px; padding: 0; justify-content: center; background: var(--field); }
.check { font-size: 11px; line-height: 1; }
.obj { color: var(--muted); }
.what { margin: 6px 0 2px 4px; color: var(--muted); font-size: 11px; }
.action {
  margin: 4px 0 0 6px; padding: 4px 6px 6px; border-radius: 3px;
  background: var(--inner); border: 1px solid var(--card-line); box-shadow: inset 3px 0 0 var(--acat);
}
.action.nested { margin-left: 4px; }
.action-title { flex: 1; min-width: 0; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.block-tag {
  display: inline-block; margin: 5px 0 1px 2px; padding: 0 5px; border-radius: 2px;
  background: #b3a400; color: #111; font-weight: 700; font-size: 11px; line-height: 16px;
}
.add {
  margin: 6px 0 0 6px; padding: 2px; border-radius: 3px; text-align: center;
  background: var(--popup); border: 1px solid var(--field-line);
}
</style>
