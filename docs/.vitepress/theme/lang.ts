import { computed } from "vue";
import { useData } from "vitepress";

/** A text in both languages, or one text for both (an object name). */
export type L = string | { ja: string; en: string };

/** "ja" or "en", following the page's locale; and t() that picks a text for it. */
export function useLang() {
  const { lang } = useData();
  const code = computed<"ja" | "en">(() => (lang.value.startsWith("ja") ? "ja" : "en"));
  const t = (text: L | undefined | null) => (text == null ? "" : typeof text === "string" ? text : text[code.value]);
  return { code, t };
}
