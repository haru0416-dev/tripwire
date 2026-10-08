// Renders the editor's icons from art/icons.json: each Lucide icon as a 32 px PNG with white lines (the editor tints
// them with the category color), into Packages/dev.haru0416.tripwire/Editor/Icons/<group>/<name>.png.
// Usage (in docs/): bun run icons [out dir]. New PNGs need a .meta like the others (Editor GUI, no compression).
import { Resvg } from "@resvg/resvg-js";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const root = join(import.meta.dir, "..", "..");
const out = process.argv[2] ?? join(root, "Packages/dev.haru0416.tripwire/Editor/Icons");
const map: Record<string, Record<string, string>> = JSON.parse(readFileSync(join(root, "art/icons.json"), "utf8"));

let n = 0;
for (const [group, icons] of Object.entries(map)) {
  if (group.startsWith("_")) continue;
  mkdirSync(join(out, group), { recursive: true });
  for (const [name, lucide] of Object.entries(icons)) {
    const svg = readFileSync(join(import.meta.dir, "..", "node_modules/lucide-static/icons", lucide + ".svg"), "utf8")
      .replace(/currentColor/g, "#FFFFFF");
    writeFileSync(join(out, group, name + ".png"), new Resvg(svg, { fitTo: { mode: "width", value: 32 } }).render().asPng());
    n++;
  }
}
console.log(`${n} icons → ${out}`);
