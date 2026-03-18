import { describe, expect, it } from "vitest";
import { createCosmosClientOptions } from "./cosmos.js";

describe("createCosmosClientOptions", () => {
  it("uses key auth when COSMOS_KEY is present", () => {
    const options = createCosmosClientOptions({
      COSMOS_ENDPOINT: "http://localhost:8081",
      COSMOS_KEY: "local-test-key",
    });

    expect(options).toMatchObject({
      endpoint: "http://localhost:8081",
      key: "local-test-key",
    });
    expect(options).not.toHaveProperty("aadCredentials");
  });

  it("fails fast for an HTTP endpoint without COSMOS_KEY", () => {
    expect(() =>
      createCosmosClientOptions({
        COSMOS_ENDPOINT: "http://localhost:8081",
      })
    ).toThrowError("COSMOS_KEY environment variable is required for HTTP Cosmos endpoints");
  });

  it("uses aad credentials when COSMOS_KEY is absent", () => {
    const options = createCosmosClientOptions({
      COSMOS_ENDPOINT: "https://prod-cosmos.documents.azure.com:443/",
    });

    expect(options).toMatchObject({
      endpoint: "https://prod-cosmos.documents.azure.com:443/",
    });
    expect(options).toHaveProperty("aadCredentials");
    expect(options).not.toHaveProperty("key");
  });
});
