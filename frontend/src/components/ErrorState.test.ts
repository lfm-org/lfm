import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createElement } from "react";
import ErrorState from "./ErrorState";

describe("ErrorState", () => {
  it("renders error message", () => {
    const html = renderToStaticMarkup(createElement(ErrorState, { message: "Network error" }));
    expect(html).toContain("Network error");
  });

  it("renders retry button when onRetry provided", () => {
    const html = renderToStaticMarkup(createElement(ErrorState, { message: "Failed", onRetry: () => {} }));
    expect(html).toContain("Try again");
  });

  it("omits retry button when onRetry not provided", () => {
    const html = renderToStaticMarkup(createElement(ErrorState, { message: "Failed" }));
    expect(html).not.toContain("Try again");
  });
});
