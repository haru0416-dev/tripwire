<script setup lang="ts">
// The top page: what Tripwire is, in one sentence, next to a real-looking card; then examples and where to go next.
import TriggerCard from "./TriggerCard.vue";
import { useLang } from "../lang";
import { withBase } from "vitepress";

const { t, code } = useLang();
const base = () => (code.value === "ja" ? "/" : "/en/");

const examples = [
  { ja: "鍵の扉", en: "Locked door", jaD: "鍵を拾った人がクリックすると開き、持っていない人には「鍵が必要です」と出す", enD: "Opens for whoever picked up the key; others see \"You need the key\"" },
  { ja: "スコアボード", en: "Scoreboard", jaD: "ボタンを押すたびに数を足して看板に出し、あとから来た人にも同じ数が届く", enD: "Each press adds to a count shown on a sign; players who join later see the same count" },
  { ja: "点滅するライト", en: "Blinking light", jaD: "3〜6 秒のランダムな間隔で表示を切り替え、スイッチで止める", enD: "Toggles every 3–6 seconds at random, stopped by a switch" },
  { ja: "シアター", en: "Theater", jaD: "ボタンごとの URL を動画プレイヤーで流し、再生が始まったら照明を落とす", enD: "Plays a URL per button on a video player and dims the lights when it starts" },
];
</script>

<template>
  <div class="home">
    <section class="top">
      <div class="intro">
        <h1><img :src="withBase('/logo.svg')" alt="" width="44" height="44" />Tripwire</h1>
        <p class="lead">
          {{ t({ ja: "VRChat ワールドのギミックを、Inspector で「いつ」と「何をする」を並べて作る Unity エディタ拡張です。並べた内容は UdonSharp に変換され、そのまま VRChat で動きます。",
                 en: "A Unity editor extension for building VRChat world gimmicks in the Inspector: pick when something happens and list what to do. It becomes UdonSharp and runs in VRChat as it is." }) }}
        </p>
        <p class="links">
          <a class="primary" :href="withBase(base() + 'guide/install')">{{ t({ ja: "入れ方", en: "Install" }) }}</a>
          <a class="secondary" :href="withBase(base() + 'guide/first-gimmick')">{{ t({ ja: "最初のギミックを作る", en: "Build your first gimmick" }) }}</a>
        </p>
        <p class="req">{{ t({ ja: "Unity 2022.3 / VRChat Worlds SDK 3.10.5 以降", en: "Unity 2022.3 / VRChat Worlds SDK 3.10.5 or later" }) }}</p>
      </div>
      <div class="demo">
        <TriggerCard event="Interact"
          :actions="[
            { id: 'GameObject.ToggleActive', rows: [{ p: 0, value: 'Mirror', kind: 'object' }] },
            { id: 'AudioSource.Play', rows: [{ p: 0, value: 'Chime', kind: 'object' }] },
          ]" />
        <p class="caption">{{ t({ ja: "クリックしたら、鏡の表示を切り替えて音を鳴らす", en: "When clicked: toggle the mirror and play a chime" }) }}</p>
      </div>
    </section>

    <!-- The intro video: a real take in the editor. Nothing loads until play (preload none); the poster is ~50 KB. -->
    <section class="video">
      <video controls playsinline preload="none" :poster="withBase(`/video/intro-${code}.jpg`)" :src="withBase(`/video/intro-${code}.mp4`)"
        :aria-label="t({ ja: 'ひな形から鏡のスイッチを作り、Play で試すまで（28 秒）', en: 'From a starter card to a working mirror switch in play mode (28 seconds)' })" />
    </section>

    <section class="examples">
      <h2>{{ t({ ja: "作れるギミックの例", en: "Gimmicks you can build" }) }}</h2>
      <ul>
        <li v-for="e in examples" :key="e.en"><strong>{{ code === "ja" ? e.ja : e.en }}</strong><span>{{ code === "ja" ? e.jaD : e.enD }}</span></li>
      </ul>
      <p class="note">{{ t({ ja: "どれもスクリプトを書かずに、一覧から選ぶだけで作れます。", en: "All of them are made by picking from lists, with no script to write." }) }}</p>
    </section>

    <section class="support">
      <p>{{ t({ ja: "Tripwire は無料です。気に入ったら、BOOTH で支援してもらえると開発の励みになります。", en: "Tripwire is free. If you like it, you can support its development on BOOTH." }) }}</p>
      <a class="secondary" href="https://haru-dev.booth.pm/items/8959507" target="_blank" rel="noopener">{{ t({ ja: "BOOTH で支援する", en: "Support on BOOTH" }) }}</a>
    </section>
  </div>
</template>

<style scoped>
.home { max-width: 1040px; margin: 0 auto; padding: 56px 24px 72px; }
.top { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 440px); gap: 48px; align-items: center; }
h1 { display: flex; align-items: center; gap: 14px; margin: 0; font-size: 40px; font-weight: 700; letter-spacing: -0.01em; line-height: 1.1; }
.lead { margin: 20px 0 0; font-size: 17px; line-height: 1.8; color: var(--vp-c-text-1); max-width: 34em; }
.links { display: flex; flex-wrap: wrap; gap: 10px; margin: 28px 0 0; }
.links a { padding: 9px 18px; border-radius: 6px; font-weight: 600; font-size: 15px; text-decoration: none; }
.primary { background: var(--vp-c-brand-1); color: var(--vp-c-white); }
:global(.dark .home .primary) { color: #0b1a24; }
.primary:hover { background: var(--vp-c-brand-2); }
.secondary { border: 1px solid var(--vp-c-divider); color: var(--vp-c-text-1); }
.secondary:hover { border-color: var(--vp-c-brand-1); color: var(--vp-c-brand-1); }
.support { margin-top: 56px; padding-top: 28px; border-top: 1px solid var(--vp-c-divider); display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px 24px; }
.support p { margin: 0; color: var(--vp-c-text-2); font-size: 15px; }
.support a { padding: 7px 16px; border-radius: 6px; font-weight: 600; font-size: 14px; text-decoration: none; }
.req { margin: 14px 0 0; font-size: 13px; color: var(--vp-c-text-2); }
.demo :deep(.tw-card) { margin: 0; max-width: none; }
.caption { margin: 10px 0 0; font-size: 13px; color: var(--vp-c-text-2); text-align: center; }

.video { margin-top: 56px; }
.video video { display: block; width: 100%; height: auto; aspect-ratio: 16 / 9; border-radius: 8px; border: 1px solid var(--vp-c-divider); background: #14181d; }

.examples { margin-top: 72px; padding-top: 32px; border-top: 1px solid var(--vp-c-divider); }
.examples h2 { margin: 0; font-size: 20px; font-weight: 600; }
.examples ul { list-style: none; margin: 16px 0 0; padding: 0; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 40px; }
.examples li { display: grid; gap: 2px; padding: 14px 0; border-bottom: 1px solid var(--vp-c-divider); }
.examples li span { color: var(--vp-c-text-2); font-size: 14px; line-height: 1.7; }
.note { margin: 16px 0 0; color: var(--vp-c-text-2); font-size: 14px; }

@media (max-width: 860px) {
  .top { grid-template-columns: minmax(0, 1fr); gap: 32px; }
  .examples ul { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 640px) {
  .home { padding: 32px 16px 56px; }
  h1 { font-size: 32px; }
}
</style>
