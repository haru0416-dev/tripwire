<script setup lang="ts">
// The page of one event or action, built from the editor's catalog. The page's markdown holds the title, the
// description and any hand-written note (so search finds them); this component draws the rest, in two parts:
//   part="head": category and Udon name (under the title)
//   part="needs": what the event needs on its object (after the description)
//   part="body": values or settings, a card using it, the UdonSharp it becomes, and its neighbors (at the end)
import { computed } from "vue";
import { withBase } from "vitepress";
import Icon from "./Icon.vue";
import TriggerCard from "./TriggerCard.vue";
import { useLang } from "../lang";
import {
  byKind, category, categoryColor, categoryIcon, eventIcon, actionIcon, slug,
  type EventEntry, type ActionEntry,
} from "../catalog";

const props = defineProps<{ kind: "event" | "action"; id: string; part: "head" | "needs" | "body" }>();
const { t, code } = useLang();

const entry = computed(() => byKind(props.kind).find((e) => e.id === props.id)!);
const ev = computed(() => (props.kind === "event" ? (entry.value as EventEntry) : null));
const ac = computed(() => (props.kind === "action" ? (entry.value as ActionEntry) : null));
const cat = computed(() => category(entry.value.category, props.kind)!);
const color = computed(() => categoryColor(entry.value.category));
const icon = computed(() => (props.kind === "event" ? eventIcon(props.id) : actionIcon(props.id)));
const root = computed(() => (code.value === "ja" ? "/" : "/en/") + "reference/" + (props.kind === "event" ? "events" : "actions"));

const needText: Record<string, { ja: string; en: string }> = {
  Collider: { ja: "このオブジェクトにコライダーが必要です。カードの「コライダーを付ける」で付けられます。", en: "Needs a collider on this object; **Add a collider** on the card adds one." },
  Area: { ja: "このオブジェクトに範囲（Is Trigger をオンにしたコライダー）が必要です。カードの「範囲を付ける」で付けられます。", en: "Needs an area on this object (a collider with Is Trigger on); **Add an area** on the card adds one." },
  Pickup: { ja: "このオブジェクトに VRC Pickup が必要です。カードのボタンで付けられます。", en: "Needs a VRC Pickup on this object; the card's button adds one." },
  Station: { ja: "このオブジェクトに VRC Station が必要です。カードのボタンで付けられます。", en: "Needs a VRC Station on this object; the card's button adds one." },
  VideoPlayer: { ja: "動画プレイヤーと同じオブジェクトに付けたトリガーにしか届きません。", en: "Only reaches a trigger on the video player's own object." },
  UiComponent: { ja: "UI の部品（ボタンなど）を選びます。VRChat で押すには、Canvas に VRC Ui Shape が必要です。", en: "Pick the UI element (a button...). To press it in VRChat, its Canvas needs a VRC Ui Shape." },
};

// The example card: the event alone, or the action under a click with its settings as placeholders.
const exampleActions = computed(() => {
  if (!ac.value) return [];
  const isObject = (en: string) => /list|Object|Transform|Animator|Audio|Particle|TMP|Collider|Renderer|Behaviour|Pickup|Video|Udon/.test(en);
  const picks = (en: string) => en === "Variable" || en === "Timer" || en === "On/Off";
  const rows = ac.value.parameters.map((p, i) => ({
    p: i,
    value: isObject(p.type.en) ? { ja: "（なし）", en: "None" }
      : p.type.en === "On/Off" ? (p.default === "false" ? { ja: "オフ", en: "Off" } : { ja: "オン", en: "On" })
      : picks(p.type.en) ? { ja: "（選ぶ）", en: "(pick)" }
      : p.default ?? "",
    kind: isObject(p.type.en) ? ("object" as const) : picks(p.type.en) ? ("popup" as const) : ("text" as const),
  }));
  return [{ id: ac.value.id, rows, ...(ac.value.holdsActions ? { then: [], ...(ac.value.id === "Flow.If" ? { else: [] } : {}) } : {}) }];
});

const udon = computed(() => {
  if (ev.value) return ev.value.signature;
  if (!ac.value?.template) return null;
  return ac.value.template;
});

const neighbors = computed(() => byKind(props.kind).filter((e) => e.category === entry.value.category && e.id !== props.id));
const md = (s: string) => s.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
</script>

<template>
  <div v-if="part === 'head'" class="tw-entry head" :style="{ '--cat': color }">
    <p class="crumb">
      <a :href="withBase(root + '#' + slug(cat.id))"><Icon :name="categoryIcon(cat.id)" :color="color" :size="13" />{{ t(cat.name) }}</a>
      <span v-if="cat.advanced" class="adv">{{ t({ ja: "くわしく", en: "Detailed" }) }}</span>
      <code v-if="entry.code !== t(entry.name)" class="code-name">{{ entry.code }}</code>
    </p>
  </div>

  <div v-else-if="part === 'needs'" class="tw-entry needs-part">
    <div v-if="ev && ev.needs.length" class="custom-block info">
      <p v-for="n in ev.needs" :key="n" v-html="md(t(needText[n]))" />
    </div>
    <div v-if="ev && ev.frequent" class="custom-block warning">
      <p>{{ t({ ja: "何度も起きるイベントです。重い処理は入れず、詳細設定は「自分だけ（Local）」のままにしてください。", en: "This event happens often: keep heavy work out of it, and keep it on Only my screen." }) }}</p>
    </div>
  </div>

  <div v-else class="tw-entry body" :style="{ '--cat': color }">
    <template v-if="ev && ev.values.length">
      <h2>{{ t({ ja: "イベントの値", en: "Event values" }) }}</h2>
      <p>{{ t({ ja: "このイベントが持つ値です。アクションの設定で、対象や値として選べます。", en: "Values this event has; action settings can use them as targets or values." }) }}</p>
      <table>
        <thead><tr><th>{{ t({ ja: "名前", en: "Name" }) }}</th><th>{{ t({ ja: "種類", en: "Type" }) }}</th></tr></thead>
        <tbody><tr v-for="(v, i) in ev.values" :key="i"><td>{{ t(v.name) }}</td><td>{{ t(v.type) }}</td></tr></tbody>
      </table>
    </template>

    <template v-if="ac && ac.parameters.length">
      <h2>{{ t({ ja: "設定", en: "Settings" }) }}</h2>
      <table>
        <thead><tr><th>{{ t({ ja: "名前", en: "Name" }) }}</th><th>{{ t({ ja: "種類", en: "Type" }) }}</th><th>{{ t({ ja: "最初の値", en: "Default" }) }}</th></tr></thead>
        <tbody><tr v-for="(p, i) in ac.parameters" :key="i"><td>{{ t(p.name) }}</td><td>{{ t(p.type) }}</td><td><code v-if="p.default">{{ p.default }}</code></td></tr></tbody>
      </table>
    </template>

    <h2>{{ t({ ja: "カードの例", en: "On a card" }) }}</h2>
    <TriggerCard v-if="ev" :event="entry.id" :actions="[]" />
    <TriggerCard v-else event="Interact" :actions="exampleActions" />

    <template v-if="udon">
      <h2>{{ t({ ja: "Udon では", en: "In Udon" }) }}</h2>
      <p>{{ ev ? t({ ja: "UdonSharp では、このメソッドになります。", en: "In UdonSharp, this is the method:" })
              : t({ ja: "対象ごとに、おおよそ次のコードになります（{ } の中は設定の値）。", en: "Roughly this code, once per target ({ } are the settings):" }) }}</p>
      <div class="language-cs"><pre><code>{{ udon }}</code></pre></div>
    </template>

    <template v-if="neighbors.length">
      <h2>{{ t({ ja: "同じ分類のもの", en: "In the same category" }) }}</h2>
      <ul class="neighbors">
        <li v-for="n in neighbors" :key="n.id">
          <a :href="withBase(root + '/' + slug(n.id))">
            <Icon :name="kind === 'event' ? eventIcon(n.id) : actionIcon(n.id)" :color="color" :size="15" />{{ t(n.name) }}
          </a>
        </li>
      </ul>
    </template>
  </div>
</template>

<style scoped>
.crumb { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin: 10px 0 18px; font-size: 13px; color: var(--vp-c-text-3); }
.crumb a { display: inline-flex; align-items: center; gap: 4px; color: var(--vp-c-text-2); text-decoration: none; }
.crumb a:hover { color: var(--vp-c-brand-1); }
.adv { padding: 0 6px; border-radius: 4px; border: 1px solid var(--vp-c-divider); font-size: 11.5px; line-height: 18px; color: var(--vp-c-text-2); }
.code-name { font-size: 12.5px; }
.neighbors { list-style: none; padding: 0 !important; display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 2px 16px; }
.neighbors li { margin: 0 !important; }
.neighbors a { display: inline-flex; align-items: center; gap: 6px; font-size: 14px; text-decoration: none; }
</style>
