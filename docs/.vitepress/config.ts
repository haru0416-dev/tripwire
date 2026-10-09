import { defineConfig, type DefaultTheme, type HeadConfig } from "vitepress";
import { readFileSync } from "node:fs";
import { join } from "node:path";

// The Tripwire manual. Japanese at /, English at /en/. Reference pages are built from .vitepress/data/catalog.json,
// which is written from the editor's own catalog (bun run data), so they match what the Inspector shows.

function sidebarJa(): DefaultTheme.SidebarItem[] {
  return [
    {
      text: "はじめに",
      items: [
        { text: "Tripwire とは", link: "/guide/" },
        { text: "入れ方", link: "/guide/install" },
        { text: "最初のギミック", link: "/guide/first-gimmick" },
      ],
    },
    {
      text: "組み立て方",
      items: [
        { text: "いつ → 何をする", link: "/guide/events-and-actions" },
        { text: "変数", link: "/guide/variables" },
        { text: "全員で同じ状態にする", link: "/guide/sync" },
        { text: "If・ループ・タイマー", link: "/guide/flow" },
        { text: "ほかのトリガーやスクリプト", link: "/guide/linking" },
        { text: "動画プレイヤー", link: "/guide/video-players" },
        { text: "Play で試す", link: "/guide/testing" },
        { text: "配布・販売するとき", link: "/guide/distribution" },
      ],
    },
    {
      text: "レシピ",
      collapsed: false,
      items: [
        { text: "鏡の切り替え", link: "/recipes/mirror" },
        { text: "全員で共有するスイッチ", link: "/recipes/shared-switch" },
        { text: "テレポート", link: "/recipes/teleport" },
        { text: "鍵の扉", link: "/recipes/locked-door" },
        { text: "スコアボード", link: "/recipes/scoreboard" },
        { text: "点滅するライト", link: "/recipes/blinking-light" },
        { text: "サイコロ", link: "/recipes/dice" },
        { text: "シアター", link: "/recipes/theater" },
      ],
    },
    {
      text: "困ったとき",
      items: [
        { text: "エラーと注意", link: "/help/problems" },
        { text: "よくある質問", link: "/help/faq" },
        { text: "用語集", link: "/help/glossary" },
      ],
    },
    {
      text: "一覧",
      items: [
        { text: "イベント", link: "/reference/events" },
        { text: "アクション", link: "/reference/actions" },
      ],
    },
  ];
}

function sidebarEn(): DefaultTheme.SidebarItem[] {
  return [
    {
      text: "Getting started",
      items: [
        { text: "What is Tripwire", link: "/en/guide/" },
        { text: "Install", link: "/en/guide/install" },
        { text: "Your first gimmick", link: "/en/guide/first-gimmick" },
      ],
    },
    {
      text: "Building",
      items: [
        { text: "When → What to do", link: "/en/guide/events-and-actions" },
        { text: "Variables", link: "/en/guide/variables" },
        { text: "The same state for everyone", link: "/en/guide/sync" },
        { text: "If, loops, timers", link: "/en/guide/flow" },
        { text: "Other triggers and scripts", link: "/en/guide/linking" },
        { text: "Video players", link: "/en/guide/video-players" },
        { text: "Trying it in Play", link: "/en/guide/testing" },
        { text: "Distributing and selling", link: "/en/guide/distribution" },
      ],
    },
    {
      text: "Recipes",
      collapsed: false,
      items: [
        { text: "Mirror switch", link: "/en/recipes/mirror" },
        { text: "A switch everyone shares", link: "/en/recipes/shared-switch" },
        { text: "Teleport", link: "/en/recipes/teleport" },
        { text: "Locked door", link: "/en/recipes/locked-door" },
        { text: "Scoreboard", link: "/en/recipes/scoreboard" },
        { text: "Blinking light", link: "/en/recipes/blinking-light" },
        { text: "Dice", link: "/en/recipes/dice" },
        { text: "Theater", link: "/en/recipes/theater" },
      ],
    },
    {
      text: "Help",
      items: [
        { text: "Errors and warnings", link: "/en/help/problems" },
        { text: "FAQ", link: "/en/help/faq" },
        { text: "Glossary", link: "/en/help/glossary" },
      ],
    },
    {
      text: "Reference",
      items: [
        { text: "Events", link: "/en/reference/events" },
        { text: "Actions", link: "/en/reference/actions" },
      ],
    },
  ];
}

// Where the site is served: "/" on its own domain, "/tripwire/" on GitHub Pages' project URL (set by the workflow).
const base = process.env.DOCS_BASE ?? "/";
// The site's origin, for link cards (Open Graph needs absolute URLs).
const origin = process.env.DOCS_ORIGIN ?? "https://haru0416-dev.github.io";

const siteDescription = {
  ja: "VRChat のギミックを Inspector で組み立てる Unity エディタ拡張",
  en: "A Unity editor extension for building VRChat gimmicks in the Inspector",
};

// Each event and action page describes itself with its one-line description from the catalog.
const catalog = JSON.parse(readFileSync(join(__dirname, "data", "catalog.json"), "utf8"));
const entryDescriptions = new Map<string, { ja: string; en: string }>(
  [...catalog.events, ...catalog.actions].map((e: { id: string; description: { ja: string; en: string } }) => [e.id, e.description]),
);

/** Link-card tags for a page: its title and description, the site's card image in its language. */
function linkCard(page: { title: string; description: string; relativePath: string; params?: Record<string, string> }): HeadConfig[] {
  const lang = page.relativePath.startsWith("en/") ? "en" : "ja";
  const path = page.relativePath.replace(/(^|\/)index\.md$/, "$1").replace(/\.md$/, "");
  const url = origin + base + path;
  const title = page.title && page.title !== "Tripwire" ? page.title + " | Tripwire" : "Tripwire";
  const description = (page.params?.key && entryDescriptions.get(page.params.key)?.[lang]) || page.description || siteDescription[lang];
  const image = origin + base + "og-" + lang + ".png";
  return [
    ["meta", { property: "og:type", content: path === "" || path === "en/" ? "website" : "article" }],
    ["meta", { property: "og:site_name", content: "Tripwire" }],
    ["meta", { property: "og:title", content: title }],
    ["meta", { property: "og:description", content: description }],
    ["meta", { property: "og:url", content: url }],
    ["meta", { property: "og:image", content: image }],
    ["meta", { property: "og:image:width", content: "1200" }],
    ["meta", { property: "og:image:height", content: "630" }],
    ["meta", { property: "og:locale", content: lang === "ja" ? "ja_JP" : "en_US" }],
    ["meta", { name: "twitter:card", content: "summary_large_image" }],
    ["meta", { name: "twitter:title", content: title }],
    ["meta", { name: "twitter:description", content: description }],
    ["meta", { name: "twitter:image", content: image }],
  ];
}

export default defineConfig({
  title: "Tripwire",
  base,
  cleanUrls: true,
  lastUpdated: false,
  head: [
    ["link", { rel: "icon", type: "image/svg+xml", href: base + "logo.svg" }],
    ["link", { rel: "preconnect", href: "https://fonts.googleapis.com" }],
    ["link", { rel: "preconnect", href: "https://fonts.gstatic.com", crossorigin: "" }],
    ["link", { rel: "stylesheet", href: "https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@400;500;700&family=JetBrains+Mono:wght@400;500&display=swap" }],
  ],
  srcExclude: ["README.md", "node_modules/**"],
  transformHead: ({ pageData }) => linkCard(pageData),

  locales: {
    root: {
      label: "日本語",
      lang: "ja-JP",
      description: "VRChat のギミックを Inspector で組み立てる Unity エディタ拡張",
      themeConfig: {
        nav: [
          { text: "ガイド", link: "/guide/", activeMatch: "/guide/" },
          { text: "レシピ", link: "/recipes/mirror", activeMatch: "/recipes/" },
          { text: "一覧", link: "/reference/events", activeMatch: "/reference/" },
        ],
        sidebar: sidebarJa(),
        outline: { label: "このページの内容" },
        docFooter: { prev: "前のページ", next: "次のページ" },
        darkModeSwitchLabel: "テーマ",
        lightModeSwitchTitle: "ライトテーマにする",
        darkModeSwitchTitle: "ダークテーマにする",
        sidebarMenuLabel: "目次",
        returnToTopLabel: "ページの先頭へ",
        langMenuLabel: "言語",
        notFound: { title: "ページが見つかりません", quote: "", linkLabel: "トップへ", linkText: "トップへ戻る" },
      },
    },
    en: {
      label: "English",
      lang: "en-US",
      link: "/en/",
      description: "A Unity editor extension for building VRChat gimmicks in the Inspector",
      themeConfig: {
        nav: [
          { text: "Guide", link: "/en/guide/", activeMatch: "/en/guide/" },
          { text: "Recipes", link: "/en/recipes/mirror", activeMatch: "/en/recipes/" },
          { text: "Reference", link: "/en/reference/events", activeMatch: "/en/reference/" },
        ],
        sidebar: sidebarEn(),
      },
    },
  },

  themeConfig: {
    logo: { src: "/logo.svg", alt: "" },
    socialLinks: [{ icon: "github", link: "https://github.com/haru0416-dev/tripwire" }],
    search: {
      provider: "local",
      options: {
        locales: {
          root: {
            translations: {
              button: { buttonText: "検索", buttonAriaLabel: "検索" },
              modal: {
                displayDetails: "詳しく表示",
                resetButtonTitle: "検索をクリア",
                backButtonTitle: "閉じる",
                noResultsText: "見つかりませんでした",
                footer: { selectText: "選ぶ", navigateText: "移動", closeText: "閉じる" },
              },
            },
          },
        },
      },
    },
  },
});
