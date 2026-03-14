import { buildApiUrl } from "./ApiUtil";

describe("buildApiUrl", () => {
  describe("given any API path", () => {
    it("then prepends /api", () => {
      expect(buildApiUrl("/raids")).toBe("/api/raids");
    });

    it("then preserves nested paths", () => {
      expect(buildApiUrl("/raids/42")).toBe("/api/raids/42");
    });
  });
});
