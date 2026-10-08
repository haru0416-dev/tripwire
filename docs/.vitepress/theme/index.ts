// The site theme: VitePress's default theme with Tripwire's colors and components.
import DefaultTheme from "vitepress/theme";
import type { Theme } from "vitepress";
import Icon from "./components/Icon.vue";
import TriggerCard from "./components/TriggerCard.vue";
import CatalogList from "./components/CatalogList.vue";
import CatalogEntry from "./components/CatalogEntry.vue";
import Home from "./components/Home.vue";
import "./custom.css";

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component("Icon", Icon);
    app.component("TriggerCard", TriggerCard);
    app.component("CatalogList", CatalogList);
    app.component("CatalogEntry", CatalogEntry);
    app.component("Home", Home);
  },
} satisfies Theme;
