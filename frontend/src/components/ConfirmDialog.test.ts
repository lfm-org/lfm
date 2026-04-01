import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createElement } from "react";
import ConfirmDialog from "./ConfirmDialog";

// MUI Dialog uses a Portal so its children do not appear in renderToStaticMarkup
// output. These tests verify the component accepts props and renders without error.
describe("ConfirmDialog", () => {
  const defaultProps = {
    open: true,
    title: "Delete item?",
    description: "This cannot be undone.",
    confirmLabel: "Delete",
    cancelLabel: "Cancel",
    onConfirm: () => {},
    onCancel: () => {},
  };

  it("renders without throwing", () => {
    expect(() =>
      renderToStaticMarkup(createElement(ConfirmDialog, defaultProps))
    ).not.toThrow();
  });

  it("renders without throwing when closed", () => {
    expect(() =>
      renderToStaticMarkup(createElement(ConfirmDialog, { ...defaultProps, open: false }))
    ).not.toThrow();
  });

  it("renders without throwing with loading state", () => {
    expect(() =>
      renderToStaticMarkup(createElement(ConfirmDialog, { ...defaultProps, loading: true }))
    ).not.toThrow();
  });
});
