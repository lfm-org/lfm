/**
 * @jest-environment node
 */
import { GET } from "./route";

describe("GET /api/health", () => {
  describe("given the service is running", () => {
    it("then returns status ok", async () => {
      const response = await GET();
      const body = await response.json();

      expect(response.status).toBe(200);
      expect(body.status).toBe("ok");
    });

    it("then includes a timestamp", async () => {
      const response = await GET();
      const body = await response.json();

      expect(typeof body.timestamp).toBe("string");
      expect(() => new Date(body.timestamp)).not.toThrow();
    });
  });
});
