import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import SpecIcon from "./SpecIcon";

describe("SpecIcon", () => {
  it("renders an img with correct alt and title when iconUrl is provided", () => {
    const html = renderToStaticMarkup(
      SpecIcon({ specName: "Holy", wowClassName: "Paladin", iconUrl: "https://example.test/icon.jpg" })
    );
    expect(html).toContain('<img');
    expect(html).toContain('alt="Holy Paladin"');
    expect(html).toContain('title="Holy Paladin"');
    expect(html).toContain('src="https://example.test/icon.jpg"');
  });

  it("renders a fallback when iconUrl is null", () => {
    const html = renderToStaticMarkup(
      SpecIcon({ specName: "Protection", wowClassName: "Warrior", iconUrl: null })
    );
    expect(html).not.toContain('<img');
    expect(html).toContain("P");
    expect(html).toContain('aria-label="Protection Warrior"');
  });

  it("applies custom size", () => {
    const html = renderToStaticMarkup(
      SpecIcon({ specName: "Holy", wowClassName: "Paladin", iconUrl: "https://example.test/icon.jpg", size: 16 })
    );
    expect(html).toContain("16");
  });
});
