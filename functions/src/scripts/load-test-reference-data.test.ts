import { describe, expect, it } from "vitest";
import { assertLocalReferenceDataEnvironment } from "./load-test-reference-data.js";

describe("assertLocalReferenceDataEnvironment", () => {
  it("refuses to run outside the intended local test environment", () => {
    expect(() =>
      assertLocalReferenceDataEnvironment({
        TEST_MODE: "false",
        COSMOS_ENDPOINT: "http://localhost:8081",
      })
    ).toThrowError("load-test-reference-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");

    expect(() =>
      assertLocalReferenceDataEnvironment({
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://example.test:8081",
      })
    ).toThrowError("load-test-reference-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");

    expect(() =>
      assertLocalReferenceDataEnvironment({
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://cosmosdb:8081",
      })
    ).not.toThrow();
  });
});
