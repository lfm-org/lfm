import { Box } from "@mui/material";
import { isValidElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: "en" } }),
}));

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
    expect(markup).toContain("forgetMe.title");
    expect(markup).toContain("forgetMe.confirmLabel");
    expect(markup).toContain("forgetMe.undoWarning");
  });
});
