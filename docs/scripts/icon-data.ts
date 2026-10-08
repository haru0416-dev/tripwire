// Writes .vitepress/data/icons.json: the SVG of every Lucide icon the site uses (art/icons.json, the logo, and the
// page's own), so the site bundles a few dozen icons instead of all of Lucide.
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const root = join(import.meta.dir, "..", "..");
const map: Record<string, Record<string, string>> = JSON.parse(readFileSync(join(root, "art/icons.json"), "utf8"));
// Icons the site itself uses (cards, buttons, notes).
const extra = ["search", "layers", "tag", "arrow-left", "grip-horizontal", "chevron-down", "ellipsis", "circle-dot", "plus", "triangle-alert", "circle-x",
  "package", "square-plus", "copy", "check", "book-open", "arrow-right", "github", "info", "lightbulb", "sparkles"];

const names = new Set<string>(extra);
for (const [group, icons] of Object.entries(map)) if (!group.startsWith("_")) for (const n of Object.values(icons)) names.add(n);

const svgs: Record<string, string> = {};
for (const n of [...names].sort()) {
  const svg = readFileSync(join(import.meta.dir, "..", "node_modules/lucide-static/icons", n + ".svg"), "utf8");
  svgs[n] = svg.replace(/<!--[\s\S]*?-->/, "").replace(/>\s*(.*)<\/svg>/s, ">$1</svg>").match(/>([\s\S]*)<\/svg>/)![1].replace(/\s+/g, " ").trim();
}
const out = { map: Object.fromEntries(Object.entries(map).filter(([g]) => !g.startsWith("_"))), svgs };
writeFileSync(join(import.meta.dir, "..", ".vitepress/data/icons.json"), JSON.stringify(out, null, 1) + "\n");
console.log(`${Object.keys(svgs).length} icons → .vitepress/data/icons.json`);
