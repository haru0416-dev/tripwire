// Pages for each event and action (VitePress dynamic routes, see reference/events/[id].paths.ts and friends).
// The markdown of a page is its title, its description and the hand-written note in .vitepress/notes/<lang>/<slug>.md
// when there is one, so local search finds them; CatalogEntry draws the rest from the same catalog.
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

type Both = { ja: string; en: string };
type Entry = { id: string; name: Both; description: Both };

const data = join(__dirname, "data", "catalog.json");
export const watch = [data, join(__dirname, "notes", "**", "*.md")];

const slug = (id: string) => id.replace(/[^A-Za-z0-9]+/g, "-").replace(/^-|-$/g, "").toLowerCase();

export function entryPaths(kind: "event" | "action", lang: "ja" | "en") {
  const catalog = JSON.parse(readFileSync(data, "utf8"));
  const entries: Entry[] = kind === "event" ? catalog.events : catalog.actions;
  return entries.map((e) => {
    const notePath = join(__dirname, "notes", lang, slug(e.id) + ".md");
    const note = existsSync(notePath) ? readFileSync(notePath, "utf8").trim() : "";
    return {
      params: { id: slug(e.id), key: e.id },
      content: [
        `# ${e.name[lang]}`,
        `<CatalogEntry kind="${kind}" id="${e.id}" part="head" />`,
        e.description[lang],
        `<CatalogEntry kind="${kind}" id="${e.id}" part="needs" />`,
        note,
        `<CatalogEntry kind="${kind}" id="${e.id}" part="body" />`,
      ].filter(Boolean).join("\n\n") + "\n",
    };
  });
}
