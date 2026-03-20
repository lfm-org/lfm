import { describe, expect, it } from "vitest";
import {
  RESET_CONTAINER_IDS,
  WOW_BLOB_CONTAINER_NAME,
} from "./reset-storage.js";

describe("reset-storage targets", () => {
  it("declares the storage surfaces that must be cleared for the raw Blizzard schema cut", () => {
    expect(RESET_CONTAINER_IDS).toEqual(["raiders", "raids"]);
    expect(WOW_BLOB_CONTAINER_NAME).toBe("wow");
  });
});
