import catalog from "../data/catalog.json";
import icons from "../data/icons.json";

export type Both = { ja: string; en: string };
export type Category = { id: string; kind: "event" | "action"; name: Both; advanced: boolean };
export type EventEntry = {
  id: string; category: string; code: string; name: Both; description: Both;
  values: { name: Both; type: Both }[]; frequent: boolean; usesName: boolean;
  signature: string | null; needs: string[];
};
export type ActionEntry = {
  id: string; category: string; code: string; name: Both; description: Both;
  parameters: { name: Both; type: Both; default: string | null }[]; holdsActions: boolean; hasConditions: boolean;
  template: string | null;
};

export const categories = catalog.categories as Category[];
export const events = catalog.events as EventEntry[];
export const actions = catalog.actions as ActionEntry[];
export const eventById = (id: string) => events.find((e) => e.id === id);
export const actionById = (id: string) => actions.find((a) => a.id === id);
export const category = (id: string, kind: "event" | "action") => categories.find((c) => c.id === id && c.kind === kind);

/** The category colors of the editor (Okabe–Ito, readable with color-vision differences). */
export function categoryColor(id: string | undefined): string {
  switch (id) {
    case "Common": case "Show": return "#0072B2";
    case "Pickup": case "SoundFx": return "#E69F00";
    case "Player": case "Link": return "#009E73";
    case "Physics": case "Move": return "#56B4E9";
    case "Avatar": case "Variable": case "Network": return "#CC79A7";
    case "Input": case "Flow": return "#D9C700";
    case "UI": case "Text": return "#8C6BC8";
    case "Video": return "#D55E00";
    default: return "#8A8A8A";
  }
}

const map = icons.map as Record<string, Record<string, string>>;
const svgs = icons.svgs as Record<string, string>;

/** The inner SVG of a Lucide icon by its name. */
export const iconSvg = (lucide: string) => svgs[lucide] ?? "";
/** The Lucide icon the editor uses for an event (its own, else its category's). */
export const eventIcon = (id: string) => map.Events[id] ?? map.Categories[eventById(id)?.category ?? ""] ?? "circle-dot";
/** The Lucide icon the editor uses for an action (its own, else its category's). */
export const actionIcon = (id: string) => map.Actions[id] ?? map.Categories[actionById(id)?.category ?? ""] ?? "circle-dot";
export const categoryIcon = (id: string) => map.Categories[id] ?? "circle-dot";

/** The URL slug of an event or action id ("GameObject.SetActive" → "gameobject-setactive"). */
export const slug = (id: string) => id.replace(/[^A-Za-z0-9]+/g, "-").replace(/^-|-$/g, "").toLowerCase();
export const byKind = (kind: "event" | "action") => (kind === "event" ? events : actions) as (EventEntry | ActionEntry)[];
