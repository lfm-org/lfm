import { describe, expect, it } from "vitest";
import { resolveLocalTestModeAuth } from "./auth.js";
import { TEST_MODE_IDENTITY } from "./test-mode.js";

const enabledEnv = {
  TEST_MODE: "true",
  COSMOS_ENDPOINT: "http://localhost:8081",
};

describe("resolveLocalTestModeAuth", () => {
  it("accepts the plain deterministic cookie only under the local test-mode guard", () => {
    expect(
      resolveLocalTestModeAuth("foo=bar; battlenet_token=test_battlenet_token", enabledEnv)
    ).toEqual({
      identity: TEST_MODE_IDENTITY,
      accessToken: "test_battlenet_token",
    });

    expect(
      resolveLocalTestModeAuth("battlenet_token=test_battlenet_token", {
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "https://localhost:8081",
      })
    ).toBeNull();
  });
});
