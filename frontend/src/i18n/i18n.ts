import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./locales/en.json";
import fi from "./locales/fi.json";

const supportedLngs = ["en", "fi"] as const;
export type SupportedLocale = (typeof supportedLngs)[number];

export function isSupportedLocale(value: string): value is SupportedLocale {
  return (supportedLngs as readonly string[]).includes(value);
}

function detectBrowserLocale(): SupportedLocale {
  const nav = navigator.language;
  return nav.startsWith("fi") ? "fi" : "en";
}

i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    fi: { translation: fi },
  },
  lng: detectBrowserLocale(),
  fallbackLng: "en",
  supportedLngs: [...supportedLngs],
  interpolation: { escapeValue: false },
});

function syncHtmlLang(lng: string) {
  if (typeof document !== "undefined") {
    document.documentElement.lang = lng;
  }
}

syncHtmlLang(i18n.language);
i18n.on("languageChanged", syncHtmlLang);

export default i18n;
