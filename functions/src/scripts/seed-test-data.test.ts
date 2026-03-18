import { describe, expect, it } from "vitest";
import { RAIDERS_CONTAINER_DEFINITION, RAIDS_CONTAINER_DEFINITION } from "./seed-test-data.js";

describe("seed-test-data container definitions", () => {
  it("declares Hash partition keys for the emulator container bootstrap", () => {
    expect(RAIDERS_CONTAINER_DEFINITION).toEqual({
      id: "raiders",
      partitionKey: {
        paths: ["/battleNetId"],
        kind: "Hash",
      },
    });
    expect(RAIDS_CONTAINER_DEFINITION).toEqual({
      id: "raids",
      partitionKey: {
        paths: ["/id"],
        kind: "Hash",
      },
    });
  });
});
