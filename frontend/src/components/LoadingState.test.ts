import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createElement } from "react";
import LoadingState from "./LoadingState";

describe("LoadingState", () => {
  it("renders default loading text", () => {
    const html = renderToStaticMarkup(createElement(LoadingState));
    expect(html).toContain("Loading...");
  });

  it("renders custom label", () => {
    const html = renderToStaticMarkup(createElement(LoadingState, { label: "Fetching raids..." }));
    expect(html).toContain("Fetching raids...");
  });
});
