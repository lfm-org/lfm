import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import GuildSettingsEditor from "./GuildSettingsEditor";

describe("GuildSettingsEditor", () => {
  it("does not render the rank permissions section when there are no permissions", () => {
    const markup = renderToStaticMarkup(
      createElement(GuildSettingsEditor, {
        timezone: "Europe/Helsinki",
        slogan: "",
        rankPermissions: [],
        saving: false,
        rankDataFresh: true,
        onTimezoneChange: () => {},
        onSloganChange: () => {},
        onPermissionChange: () => {},
        onSave: () => {},
      }),
    );

    expect(markup).not.toContain("Rank permissions");
  });

  it("renders the slogan field with the current slogan", () => {
    const markup = renderToStaticMarkup(
      createElement(GuildSettingsEditor, {
        timezone: "Europe/Helsinki",
        slogan: "Victory or Lunch",
        rankPermissions: [],
        saving: false,
        rankDataFresh: true,
        onTimezoneChange: () => {},
        onSloganChange: () => {},
        onPermissionChange: () => {},
        onSave: () => {},
      }),
    );

    expect(markup).toContain("Slogan");
    expect(markup).toContain("Victory or Lunch");
  });
});
