import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createElement } from "react";
import { ToastProvider } from "./ToastContext";

describe("ToastProvider", () => {
  it("renders children", () => {
    const html = renderToStaticMarkup(
      createElement(ToastProvider, null, createElement("div", null, "child"))
    );
    expect(html).toContain("child");
  });
});
