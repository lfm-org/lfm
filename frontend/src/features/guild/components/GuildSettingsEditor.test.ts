import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import GuildSettingsEditor from "./GuildSettingsEditor";

describe("GuildSettingsEditor", () => {
  it("does not render the rank permissions section when there are no permissions", () => {
    const markup = renderToStaticMarkup(
      createElement(GuildSettingsEditor, {
        timezone: "Europe/Helsinki",
        rankPermissions: [],
        saving: false,
        rankDataFresh: true,
        onTimezoneChange: () => {},
        onPermissionChange: () => {},
        onSave: () => {},
      }),
    );

    expect(markup).not.toContain("Rank permissions");
  });
});
