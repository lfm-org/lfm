import test from "node:test";
import assert from "node:assert/strict";

import {
  buildExecutionPlan,
  buildDockerCommandEnvironment,
  isRetryableFunctionsScriptOutput,
  retryCommandResult,
  buildRuntimeProfile,
  resolveCliArgs,
} from "./dev-env.mjs";

test("resolveCliArgs defaults to serve when no subcommand is provided", () => {
  assert.deepEqual(resolveCliArgs([]), {
    command: "serve",
    scenario: "default",
    passthroughArgs: [],
  });
});

test("resolveCliArgs preserves e2e scenario and passthrough Playwright args", () => {
  assert.deepEqual(resolveCliArgs(["test", "raids-error", "signup.spec.ts", "--grep", "tank"]), {
    command: "test",
    scenario: "raids-error",
    passthroughArgs: ["signup.spec.ts", "--grep", "tank"],
  });
});

test("buildRuntimeProfile isolates serve and test resources so they can run concurrently", () => {
  const rootDir = "/repo";
  const serve = buildRuntimeProfile(rootDir, "serve");
  const e2e = buildRuntimeProfile(rootDir, "test");

  assert.equal(serve.composeFile, "/repo/docker-compose.local.yml");
  assert.equal(e2e.composeFile, "/repo/docker-compose.test.yml");

  assert.notEqual(serve.composeProjectName, e2e.composeProjectName);
  assert.notEqual(serve.tmpDir, e2e.tmpDir);
  assert.notEqual(serve.azuriteDataDir, e2e.azuriteDataDir);
  assert.notEqual(serve.cosmosKeyFile, e2e.cosmosKeyFile);

  assert.notEqual(serve.ports.functions, e2e.ports.functions);
  assert.notEqual(serve.ports.frontend, e2e.ports.frontend);
  assert.notEqual(serve.ports.cosmos, e2e.ports.cosmos);
  assert.notEqual(serve.ports.azuriteBlob, e2e.ports.azuriteBlob);
});

test("buildRuntimeProfile keeps e2e and dev data stores separate", () => {
  const rootDir = "/repo";
  const serve = buildRuntimeProfile(rootDir, "serve");
  const e2e = buildRuntimeProfile(rootDir, "test");

  assert.equal(serve.cosmosDatabase, "lfm-dev");
  assert.equal(e2e.cosmosDatabase, "lfm-e2e");
  assert.equal(serve.env.TEST_MODE, undefined);
  assert.equal(e2e.env.TEST_MODE, "true");
  assert.notEqual(serve.env.COSMOS_DATABASE, e2e.env.COSMOS_DATABASE);
  assert.notEqual(serve.env.APP_BASE_URL, e2e.env.APP_BASE_URL);
  assert.notEqual(serve.env.BATTLE_NET_REDIRECT_URI, e2e.env.BATTLE_NET_REDIRECT_URI);
});

test("buildExecutionPlan uses the persistent real-auth dev flow for serve", () => {
  const plan = buildExecutionPlan("/repo", resolveCliArgs(["serve"]));

  assert.equal(plan.command, "serve");
  assert.equal(plan.profile.mode, "serve");
  assert.equal(plan.referenceDataStrategy, "live-cache");
  assert.equal(plan.seedData, false);
  assert.equal(plan.runPlaywright, false);
  assert.equal(plan.startFrontend, true);
  assert.equal(plan.keepRunning, true);
  assert.deepEqual(plan.composeServices, ["cosmosdb", "azurite", "functions"]);
});

test("buildExecutionPlan keeps e2e deterministic and isolated from dev mode", () => {
  const plan = buildExecutionPlan("/repo", resolveCliArgs(["test", "characters-empty", "signup.spec.ts"]));

  assert.equal(plan.command, "test");
  assert.equal(plan.profile.mode, "test");
  assert.equal(plan.referenceDataStrategy, "snapshot");
  assert.equal(plan.seedData, true);
  assert.equal(plan.runPlaywright, true);
  assert.equal(plan.startFrontend, true);
  assert.equal(plan.keepRunning, false);
  assert.equal(plan.scenario, "characters-empty");
  assert.deepEqual(plan.playwrightArgs, ["signup.spec.ts"]);
  assert.deepEqual(plan.composeServices, ["cosmosdb", "azurite", "functions"]);
});

test("buildExecutionPlan preserves e2e shorthand spec names for compatibility", () => {
  const plan = buildExecutionPlan("/repo", resolveCliArgs(["test", "signup"]));

  assert.deepEqual(plan.playwrightArgs, ["e2e/signup.spec.ts"]);
});

test("buildDockerCommandEnvironment defaults docker commands to the engine context", () => {
  const env = buildDockerCommandEnvironment({
    PATH: "/usr/bin",
    DOCKER_HOST: "unix:///home/user/.docker/desktop/docker.sock",
    DOCKER_CONTEXT: "desktop-linux",
    DOCKER_CERT_PATH: "/tmp/docker-certs",
    DOCKER_TLS_VERIFY: "1",
  });

  assert.equal(env.PATH, "/usr/bin");
  assert.equal(env.DOCKER_CONTEXT, "default");
  assert.equal(env.DOCKER_HOST, undefined);
  assert.equal(env.DOCKER_CERT_PATH, undefined);
  assert.equal(env.DOCKER_TLS_VERIFY, undefined);
});

test("buildDockerCommandEnvironment preserves an explicit harness docker context override", () => {
  const env = buildDockerCommandEnvironment({
    PATH: "/usr/bin",
    LFM_DOCKER_CONTEXT: "podman",
    DOCKER_CONTEXT: "desktop-linux",
  });

  assert.equal(env.DOCKER_CONTEXT, "podman");
  assert.equal(env.LFM_DOCKER_CONTEXT, undefined);
  assert.equal(env.DOCKER_HOST, undefined);
});

test("retryCommandResult retries transient Cosmos startup failures", async () => {
  let attempts = 0;
  const delays = [];

  const result = await retryCommandResult(
    async () => {
      attempts += 1;
      if (attempts === 1) {
        return {
          exitCode: 1,
          stdout: "",
          stderr: "pgcosmos extension is still starting; retry request shortly",
        };
      }

      return {
        exitCode: 0,
        stdout: "ok",
        stderr: "",
      };
    },
    {
      attempts: 3,
      delayMs: 25,
      sleepFn: async (delayMs) => {
        delays.push(delayMs);
      },
      shouldRetry: (result) => isRetryableFunctionsScriptOutput(`${result.stdout}\n${result.stderr}`),
    }
  );

  assert.equal(result.exitCode, 0);
  assert.equal(attempts, 2);
  assert.deepEqual(delays, [25]);
});

test("retryCommandResult retries transient Azurite blob failures", async () => {
  let attempts = 0;
  const delays = [];

  const result = await retryCommandResult(
    async () => {
      attempts += 1;
      if (attempts === 1) {
        return {
          exitCode: 1,
          stdout: "",
          stderr: 'RestError: {"statusCode": 500, "details": {"server": "Azurite-Blob/3.35.0"}}',
        };
      }

      return {
        exitCode: 0,
        stdout: "ok",
        stderr: "",
      };
    },
    {
      attempts: 3,
      delayMs: 25,
      sleepFn: async (delayMs) => {
        delays.push(delayMs);
      },
      shouldRetry: (result) => isRetryableFunctionsScriptOutput(`${result.stdout}\n${result.stderr}`),
    }
  );

  assert.equal(result.exitCode, 0);
  assert.equal(attempts, 2);
  assert.deepEqual(delays, [25]);
});

test("retryCommandResult retries transient Cosmos connection-refused startup failures", async () => {
  let attempts = 0;
  const delays = [];

  const result = await retryCommandResult(
    async () => {
      attempts += 1;
      if (attempts === 1) {
        return {
          exitCode: 1,
          stdout: "",
          stderr: 'RestError: connect ECONNREFUSED 172.18.0.3:8081\\n"url": "http://cosmosdb:8081"',
        };
      }

      return {
        exitCode: 0,
        stdout: "ok",
        stderr: "",
      };
    },
    {
      attempts: 3,
      delayMs: 25,
      sleepFn: async (delayMs) => {
        delays.push(delayMs);
      },
      shouldRetry: (result) => isRetryableFunctionsScriptOutput(`${result.stdout}\n${result.stderr}`),
    }
  );

  assert.equal(result.exitCode, 0);
  assert.equal(attempts, 2);
  assert.deepEqual(delays, [25]);
});

test("retryCommandResult does not retry non-retryable failures", async () => {
  let attempts = 0;

  const result = await retryCommandResult(
    async () => {
      attempts += 1;
      return {
        exitCode: 1,
        stdout: "",
        stderr: "some other failure",
      };
    },
    {
      attempts: 3,
      delayMs: 25,
      sleepFn: async () => {},
      shouldRetry: (result) => isRetryableFunctionsScriptOutput(`${result.stdout}\n${result.stderr}`),
    }
  );

  assert.equal(result.exitCode, 1);
  assert.equal(attempts, 1);
});
