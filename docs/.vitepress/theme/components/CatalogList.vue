<script setup lang="ts">
// The reference index of events or actions, drawn like Tripwire's own picker (Unity's light or dark skin, following the
// site theme): categories on the left, the chosen category's entries on the right, a search box and Simple / Detailed
// on top. Each entry opens its own page. The chosen category is kept in the URL (#category) so links and Back work.
import { computed, onMounted, ref, watch } from "vue";
import { withBase } from "vitepress";
import Icon from "./Icon.vue";
import { useLang } from "../lang";
import { categories, byKind, categoryColor, categoryIcon, eventIcon, actionIcon, slug } from "../catalog";

const props = defineProps<{ kind: "event" | "action" }>();
const { t, code } = useLang();

const query = ref("");
const detailed = ref(true);
const cats = computed(() =>
  categories.filter((c) => c.kind === props.kind && byKind(props.kind).some((e) => e.category === c.id) && (detailed.value || !c.advanced)),
);
const chosen = ref<string>(cats.value[0]?.id ?? "");
const base = computed(() => (code.value === "ja" ? "/" : "/en/") + "reference/" + (props.kind === "event" ? "events" : "actions") + "/");

onMounted(() => {
  const fromHash = cats.value.find((c) => slug(c.id) === location.hash.slice(1));
  if (fromHash) chosen.value = fromHash.id;
});
watch(cats, (list) => { if (!list.some((c) => c.id === chosen.value)) chosen.value = list[0]?.id ?? ""; });
function choose(id: string) {
  chosen.value = id;
  query.value = "";
  history.replaceState(history.state, "", "#" + slug(id));
}

const shown = computed(() => {
  const q = query.value.trim().toLowerCase();
  const all = byKind(props.kind).filter((e) => cats.value.some((c) => c.id === e.category));
  const list = q
    ? all.filter((e) => [e.name.ja, e.name.en, e.code, e.id, e.description.ja, e.description.en].some((s) => s.toLowerCase().includes(q)))
    : all.filter((e) => e.category === chosen.value);
  return list.map((e) => ({ ...e, icon: props.kind === "event" ? eventIcon(e.id) : actionIcon(e.id) }));
});
const catName = (id: string) => t(categories.find((c) => c.id === id && c.kind === props.kind)!.name);
</script>

<template>
  <div class="tw-picker">
    <div class="top">
      <label class="search">
        <Icon name="search" :size="13" />
        <input v-model="query" type="search" :aria-label="t({ ja: '探す', en: 'Search' })"
               :placeholder="t({ ja: '名前・説明・Udon での名前で探す', en: 'Search names, descriptions, Udon names' })" />
      </label>
      <span class="mode" role="group" :aria-label="t({ ja: '表示する量', en: 'How much to show' })">
        <button type="button" :class="{ on: !detailed }" @click="detailed = false">{{ t({ ja: "かんたん", en: "Simple" }) }}</button>
        <button type="button" :class="{ on: detailed }" @click="detailed = true">{{ t({ ja: "くわしく", en: "Detailed" }) }}</button>
      </span>
    </div>

    <div class="panes">
      <nav class="cats" :aria-label="t({ ja: '分類', en: 'Categories' })">
        <button v-for="c in cats" :key="c.id" type="button" :class="{ on: !query && c.id === chosen }" @click="choose(c.id)">
          <Icon :name="categoryIcon(c.id)" :color="!query && c.id === chosen ? '#fff' : categoryColor(c.id)" :size="15" />
          <span>{{ t(c.name) }}</span><span class="chev">›</span>
        </button>
      </nav>

      <div class="list">
        <p class="head">{{ query ? t({ ja: `「${query}」の検索結果 ${shown.length} 件`, en: `${shown.length} results for "${query}"` }) : catName(chosen) }}</p>
        <a v-for="e in shown" :key="e.id" class="item" :href="withBase(base + slug(e.id))">
          <Icon :name="e.icon" :color="categoryColor(e.category)" :size="16" class="icon" />
          <span class="body">
            <span class="line">
              <span class="name">{{ t(e.name) }}</span>
              <span v-if="e.code !== t(e.name)" class="note">{{ e.code }}</span>
              <span v-if="query" class="note">{{ catName(e.category) }}</span>
            </span>
            <span class="desc">{{ t(e.description) }}</span>
          </span>
        </a>
        <p v-if="shown.length === 0" class="head">{{ t({ ja: "見つかりません。", en: "Nothing found." }) }}</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Unity's skins, as the TriggerCard: light by default, dark with the site's dark theme. */
.tw-picker {
  --win: #c2c2c2; --pane: #cbcbcb; --line: #999; --field: #f0f0f0; --text: #101010; --muted: #555; --hover: #b6b6b6;
  --sel: #3a73b0; --btn: #e4e4e4; --btn-on: #a8c4e2;
  margin: 20px 0; border: 1px solid var(--line); border-radius: 3px; background: var(--win); color: var(--text);
  font: 13px/1.45 system-ui, -apple-system, "Segoe UI", "Hiragino Sans", "Noto Sans JP", sans-serif;
}
:global(.dark .tw-picker) {
  --win: #383838; --pane: #3c3c3c; --line: #232323; --field: #2a2a2a; --text: #d2d2d2; --muted: #9a9a9a; --hover: #454545;
  --sel: #2c5c87; --btn: #4a4a4a; --btn-on: #46607c;
}
.top { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 6px; border-bottom: 1px solid var(--line); }
.search {
  display: flex; align-items: center; gap: 6px; flex: 1 1 240px; padding: 0 8px; border-radius: 10px;
  border: 1px solid var(--line); background: var(--field); color: var(--muted);
}
.search input { flex: 1; min-width: 0; padding: 3px 0; border: 0; outline: none; background: none; color: var(--text); font: inherit; }
.search:focus-within { border-color: var(--sel); }
.mode { display: inline-flex; }
.mode button {
  padding: 2px 12px; border: 1px solid var(--line); background: var(--btn); color: var(--text); font: inherit; cursor: pointer;
}
.mode button:first-child { border-radius: 3px 0 0 3px; }
.mode button:last-child { border-radius: 0 3px 3px 0; border-left: 0; }
.mode button.on { background: var(--btn-on); }

.panes { display: grid; grid-template-columns: 236px minmax(0, 1fr); min-height: 380px; }
.cats { display: flex; flex-direction: column; padding: 4px 0; border-right: 1px solid var(--line); background: var(--pane); }
.cats button {
  display: flex; align-items: center; gap: 8px; padding: 3px 10px; border: 0; background: none; color: var(--text);
  font: inherit; text-align: left; cursor: pointer;
}
.cats button:hover { background: var(--hover); }
.cats button.on { background: var(--sel); color: #fff; }
.cats button span:nth-child(2) { flex: 1; min-width: 0; }
.chev { color: var(--muted); }
.cats button.on .chev { color: #fff; }

.list { padding: 4px 0 8px; max-height: 560px; overflow-y: auto; }
.head { margin: 4px 10px 2px !important; font-size: 11.5px; color: var(--muted); }
.item { display: flex; gap: 8px; padding: 5px 10px; color: var(--text) !important; text-decoration: none !important; }
.item:hover { background: var(--hover); }
.icon { margin-top: 2px; }
.body { display: grid; min-width: 0; }
.line { display: flex; flex-wrap: wrap; align-items: baseline; gap: 2px 8px; }
.name { font-weight: 700; }
.note { font-size: 11px; color: var(--muted); }
.desc { font-size: 11.5px; color: var(--muted); line-height: 1.5; }

@media (max-width: 720px) {
  .panes { grid-template-columns: minmax(0, 1fr); }
  .cats { flex-direction: row; flex-wrap: wrap; gap: 2px; padding: 6px; border-right: 0; border-bottom: 1px solid var(--line); }
  .cats button { border-radius: 3px; }
  .chev { display: none; }
}
</style>
