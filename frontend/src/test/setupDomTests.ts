import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import "../i18n/i18n";

afterEach(() => {
  cleanup();
});

function matchesQuery(query: string): boolean {
  const minWidthMatch = /min-width:\s*(\d+(?:\.\d+)?)px/.exec(query);
  const maxWidthMatch = /max-width:\s*(\d+(?:\.\d+)?)px/.exec(query);

  if (!minWidthMatch && !maxWidthMatch) return false;

  if (minWidthMatch && window.innerWidth < Number(minWidthMatch[1])) return false;
  if (maxWidthMatch && window.innerWidth > Number(maxWidthMatch[1])) return false;

  return true;
}

export function setViewportWidth(width: number) {
  Object.defineProperty(window, "innerWidth", {
    configurable: true,
    writable: true,
    value: width,
  });
  window.dispatchEvent(new Event("resize"));
}

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: matchesQuery(query),
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }),
});
