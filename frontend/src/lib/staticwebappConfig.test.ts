import { describe, expect, it } from "vitest";
import config from "../../public/staticwebapp.config.json";

describe("staticwebapp.config CSP", () => {
  it("allows the API origin for guild crest images", () => {
    const csp = config.globalHeaders?.["Content-Security-Policy"] ?? "";
    const imgSrcDirective = csp
      .split(";")
      .map((directive) => directive.trim())
      .find((directive) => directive.startsWith("img-src "));

    expect(imgSrcDirective).toBeDefined();
    expect(imgSrcDirective).toMatch(/https:\/\/[^\s]+/);
  });
});
