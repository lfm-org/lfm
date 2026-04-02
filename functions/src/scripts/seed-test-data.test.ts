import { describe, expect, it } from "vitest";
import {
  GUILDS_CONTAINER_DEFINITION,
  RAIDERS_CONTAINER_DEFINITION,
  RUNS_CONTAINER_DEFINITION,
  getRunsContainerDefinitionForScenario,
} from "./seed-test-data.js";

describe("seed-test-data container definitions", () => {
  it("declares Hash partition keys for the emulator container bootstrap", () => {
    expect(RAIDERS_CONTAINER_DEFINITION).toEqual({
      id: "raiders",
      partitionKey: {
        paths: ["/battleNetId"],
        kind: "Hash",
      },
    });
    expect(RUNS_CONTAINER_DEFINITION).toEqual({
      id: "runs",
      partitionKey: {
        paths: ["/id"],
        kind: "Hash",
      },
    });
    expect(GUILDS_CONTAINER_DEFINITION).toEqual({
      id: "guilds",
      partitionKey: {
        paths: ["/id"],
        kind: "Hash",
      },
    });
  });

  it("skips the runs container bootstrap for the raids-error scenario", () => {
    expect(getRunsContainerDefinitionForScenario("default")).toEqual(RUNS_CONTAINER_DEFINITION);
    expect(getRunsContainerDefinitionForScenario("raids-empty")).toEqual(RUNS_CONTAINER_DEFINITION);
    expect(getRunsContainerDefinitionForScenario("raids-error")).toBeNull();
  });
});
