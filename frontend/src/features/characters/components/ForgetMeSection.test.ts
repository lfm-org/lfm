import { Box } from "@mui/material";
import { isValidElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import ForgetMeSection from "./ForgetMeSection";

describe("ForgetMeSection", () => {
  it("keeps the delete flow in a subdued footer section", () => {
    const element = ForgetMeSection({
      deleteConfirmation: "",
      deleteConfirmationValid: false,
      deleteError: null,
      deleting: false,
      onDeleteConfirmationChange: () => {},
      onDeleteAccount: () => {},
    });

    expect(isValidElement(element)).toBe(true);
    expect(element.type).toBe(Box);
    expect(element.props.sx).toMatchObject({
      borderTop: 1,
      borderColor: "divider",
      pt: 3,
    });

    const markup = renderToStaticMarkup(element);
    expect(markup).toContain("Forget me");
    expect(markup).toContain("Type FORGET ME to confirm");
    expect(markup).toContain("This action cannot be undone.");
  });
});
