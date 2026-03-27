import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import GuildIdentityCard from "./GuildIdentityCard";

describe("GuildIdentityCard", () => {
  it("shows the guild crest, slogan, and shared metadata", () => {
    const markup = renderToStaticMarkup(
      createElement(GuildIdentityCard, {
        guild: {
          id: 42,
          crestUrl: "https://example.com/crest.png",
          name: "Ashen Concord",
          slogan: "Steel, tea, and clean pulls",
          realmSlug: "twisting-nether",
          realmName: "Twisting Nether",
          factionName: "Horde",
          memberCount: 83,
          achievementPoints: 12345,
          syncedMemberCount: 80,
          rankCount: 8,
        },
        metadata: createElement("span", null, "8 ranks detected"),
      }),
    );

    expect(markup).toContain("Ashen Concord");
    expect(markup).toContain("Steel, tea, and clean pulls");
    expect(markup).toContain("Twisting Nether");
    expect(markup).toContain("Horde");
    expect(markup).toContain("8 ranks detected");
  });
});
