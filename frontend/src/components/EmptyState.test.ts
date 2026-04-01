import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createElement } from "react";
import EmptyState from "./EmptyState";

const icon = createElement("svg", null);

describe("EmptyState", () => {
  it("renders message", () => {
    const html = renderToStaticMarkup(
      createElement(EmptyState, {
        icon,
        message: "No items found",
      })
    );
    expect(html).toContain("No items found");
  });

  it("renders action button when action provided", () => {
    const html = renderToStaticMarkup(
      createElement(EmptyState, {
        icon,
        message: "No items",
        action: { label: "Create item", onClick: () => {} },
      })
    );
    expect(html).toContain("Create item");
  });
});
