import { describe, expect, it } from "vitest";
import { getApiErrorMessage } from "./api";

describe("getApiErrorMessage", () => {
  it("prefers the backend error field from axios responses", () => {
    expect(
      getApiErrorMessage(
        {
          isAxiosError: true,
          response: {
            data: {
              error: "Guild rank data is stale",
            },
          },
        },
        "Failed to save guild settings",
      ),
    ).toBe("Guild rank data is stale");
  });

  it("falls back when the response body does not include an error string", () => {
    expect(
      getApiErrorMessage(
        {
          isAxiosError: true,
          response: {
            data: {
              message: "Bad request",
            },
          },
        },
        "Failed to save guild settings",
      ),
    ).toBe("Failed to save guild settings");
  });
});
