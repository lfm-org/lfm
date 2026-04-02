#!/usr/bin/env node

import { spawn } from "node:child_process";
import { once } from "node:events";
import { existsSync } from "node:fs";
import fs from "node:fs/promises";
import net from "node:net";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const ROOT_DIR = path.resolve(SCRIPT_DIR, "..");
const FRONTEND_DIR = path.join(ROOT_DIR, "frontend");
const PLAYWRIGHT_BROWSERS_PATH = path.join(ROOT_DIR, ".cache/ms-playwright");

const VALID_COMMANDS = new Set(["serve", "test", "refresh-reference", "reset", "down"]);
const E2E_SCENARIOS = new Set([
  "default",
  "runs-empty",
  "runs-error",
  "characters-empty",
  "instances-missing",
]);

const COSMOS_KEY = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
const AZURITE_ACCOUNT_KEY = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
const DEFAULT_SESSION_ENCRYPTION_KEY = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
const DEFAULT_HMAC_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

export function resolveCliArgs(argv) {
  if (argv.length === 0) {
    return {
      command: "serve",
      scenario: "default",
      passthroughArgs: [],
    };
  }

  const [command, ...rest] = argv;
  if (command !== "test") {
    return {
      command,
      scenario: "default",
      passthroughArgs: rest,
    };
  }

  const [maybeScenario, ...remaining] = rest;
  if (maybeScenario && E2E_SCENARIOS.has(maybeScenario)) {
    return {
      command: "test",
      scenario: maybeScenario,
      passthroughArgs: remaining,
    };
  }

  return {
    command: "test",
    scenario: "default",
    passthroughArgs: rest,
  };
}

export function buildRuntimeProfile(rootDir, command) {
  if (command === "test") {
    return createProfile(rootDir, {
      mode: "test",
      composeFile: "docker-compose.test.yml",
      composeProjectName: "lfm-e2e",
      tmpDir: "/tmp/lfm-e2e",
      functionsPort: 7072,
      frontendPort: 4173,
      cosmosPort: 8082,
      cosmosExplorerPort: 1235,
      azuriteBlobPort: 10001,
      cosmosDatabase: "lfm-e2e",
      publicHost: "127.0.0.1",
      testMode: true,
    });
  }

  return createProfile(rootDir, {
    mode: "serve",
    composeFile: "docker-compose.local.yml",
    composeProjectName: "lfm-dev",
    tmpDir: ".tmp/dev",
    functionsPort: 7071,
    frontendPort: 5173,
    cosmosPort: 8081,
    cosmosExplorerPort: 1234,
    azuriteBlobPort: 10000,
    cosmosDatabase: "lfm-dev",
    publicHost: "localhost",
    testMode: false,
  });
}

export function buildExecutionPlan(rootDir, args) {
  const profile = buildRuntimeProfile(rootDir, args.command);
  const playwrightArgs = normalizePlaywrightArgs(args.passthroughArgs);

  if (args.command === "test") {
    return {
      command: "test",
      profile,
      scenario: args.scenario,
      playwrightArgs,
      referenceDataStrategy: "snapshot",
      seedData: true,
      runPlaywright: true,
      startFrontend: true,
      keepRunning: false,
      composeServices: ["cosmosdb", "azurite", "functions"],
    };
  }

  if (args.command === "serve") {
    return {
      command: "serve",
      profile,
      scenario: args.scenario,
      playwrightArgs: [],
      referenceDataStrategy: "live-cache",
      seedData: false,
      runPlaywright: false,
      startFrontend: true,
      keepRunning: true,
      composeServices: ["cosmosdb", "azurite", "functions"],
    };
  }

  if (args.command === "refresh-reference") {
    return {
      command: "refresh-reference",
      profile,
      scenario: "default",
      playwrightArgs: [],
      referenceDataStrategy: "live-refresh",
      seedData: false,
      runPlaywright: false,
      startFrontend: false,
      keepRunning: false,
      composeServices: ["cosmosdb", "azurite"],
    };
  }

  if (args.command === "reset") {
    return {
      command: "reset",
      profile,
      scenario: "default",
      playwrightArgs: [],
      referenceDataStrategy: "none",
      seedData: false,
      runPlaywright: false,
      startFrontend: false,
      keepRunning: false,
      composeServices: ["cosmosdb", "azurite"],
    };
  }

  return {
    command: "down",
    profile,
    scenario: "default",
    playwrightArgs: [],
    referenceDataStrategy: "none",
    seedData: false,
    runPlaywright: false,
    startFrontend: false,
    keepRunning: false,
    composeServices: [],
  };
}

async function main() {
  const args = resolveCliArgs(process.argv.slice(2));
  if (!VALID_COMMANDS.has(args.command)) {
    usage();
    process.exit(1);
  }

  const plan = buildExecutionPlan(ROOT_DIR, args);

  switch (plan.command) {
    case "serve":
      await runServe(plan);
      return;
    case "test":
      await runTest(plan);
      return;
    case "refresh-reference":
      await runRefreshReference(plan);
      return;
    case "reset":
      await runReset(plan);
      return;
    case "down":
      await runDown(plan);
      return;
    default:
      usage();
      process.exit(1);
  }
}

async function runServe(plan) {
  const runtimeEnv = await buildServeEnvironment(plan.profile);
  const composeEnv = buildComposeEnvironment(plan.profile, runtimeEnv);
  const shutdown = createShutdownTracker();
  let viteChild = null;

  try {
    await prepareProfile(plan.profile);
    shutdown.throwIfInterrupted();

    await ensureDependencies(plan.profile, composeEnv);
    shutdown.throwIfInterrupted();

    await buildFunctionsImage(plan.profile, composeEnv);
    shutdown.throwIfInterrupted();

    await runFunctionsScript(plan.profile, composeEnv, "sync-reference-data.js");
    shutdown.throwIfInterrupted();

    await startFunctionsService(plan.profile, composeEnv);
    shutdown.throwIfInterrupted();

    viteChild = spawnCommand("npm", ["run", "dev", "--", "--host", "127.0.0.1"], {
      cwd: FRONTEND_DIR,
      env: buildFrontendServeEnvironment(plan.profile),
      stdio: "inherit",
    });
    shutdown.attachChild(viteChild);

    await waitForHttp(`http://127.0.0.1:${plan.profile.ports.frontend}`);

    console.log(`Frontend: ${plan.profile.env.APP_BASE_URL}`);
    console.log(`API: http://${plan.profile.publicHost}:${plan.profile.ports.functions}/api`);
    console.log("Press Ctrl-C to stop the local dev stack.");

    const exitCode = await waitForChild(viteChild);
    if (exitCode !== 0 && !shutdown.wasInterrupted()) {
      throw new Error(`Frontend dev server exited with code ${exitCode}`);
    }
  } finally {
    shutdown.dispose();
    await terminateChild(viteChild);
    await stopComposeServices(plan.profile, composeEnv, { quiet: true });
  }
}

async function runTest(plan) {
  const runtimeEnv = buildTestEnvironment(plan.profile, plan.scenario);
  const composeEnv = buildComposeEnvironment(plan.profile, runtimeEnv);
  const shutdown = createShutdownTracker();
  let playwrightChild = null;

  try {
    await prepareProfile(plan.profile, { resetAzurite: true });
    shutdown.throwIfInterrupted();

    await ensureDependencies(plan.profile, composeEnv);
    shutdown.throwIfInterrupted();

    await buildFunctionsImage(plan.profile, composeEnv);
    shutdown.throwIfInterrupted();

    await runFunctionsScript(plan.profile, composeEnv, "load-test-reference-data.js");
    shutdown.throwIfInterrupted();

    await runFunctionsScript(plan.profile, composeEnv, "seed-test-data.js");
    shutdown.throwIfInterrupted();

    await startFunctionsService(plan.profile, composeEnv);
    shutdown.throwIfInterrupted();

    await ensurePlaywrightBrowser();
    shutdown.throwIfInterrupted();

    playwrightChild = spawnCommand("npx", ["playwright", "test", ...plan.playwrightArgs], {
      cwd: FRONTEND_DIR,
      env: buildFrontendTestEnvironment(plan.profile, plan.scenario),
      stdio: "inherit",
    });
    shutdown.attachChild(playwrightChild);

    const exitCode = await waitForChild(playwrightChild);
    if (exitCode !== 0) {
      process.exitCode = exitCode;
    }
  } finally {
    shutdown.dispose();
    await terminateChild(playwrightChild);

    if (process.env.E2E_KEEP_DOCKER !== "1") {
      await teardownComposeProject(plan.profile, composeEnv, { quiet: true });
    }
  }
}

async function runRefreshReference(plan) {
  const runtimeEnv = await buildServeEnvironment(plan.profile);
  const composeEnv = buildComposeEnvironment(plan.profile, runtimeEnv);

  await prepareProfile(plan.profile);
  await ensureDependencies(plan.profile, composeEnv);
  await buildFunctionsImage(plan.profile, composeEnv);
  await runFunctionsScript(plan.profile, composeEnv, "sync-reference-data.js", ["--force"]);
}

async function runReset(plan) {
  const runtimeEnv = buildMaintenanceEnvironment(plan.profile);
  const composeEnv = buildComposeEnvironment(plan.profile, runtimeEnv);

  await prepareProfile(plan.profile);
  await ensureDependencies(plan.profile, composeEnv);
  await buildFunctionsImage(plan.profile, composeEnv);
  await runFunctionsScript(plan.profile, composeEnv, "reset-storage.js");
}

async function runDown(plan) {
  const composeEnv = buildComposeEnvironment(plan.profile, buildMaintenanceEnvironment(plan.profile));
  await stopComposeServices(plan.profile, composeEnv);
}

function createProfile(rootDir, config) {
  const tmpDir = path.isAbsolute(config.tmpDir) ? config.tmpDir : path.join(rootDir, config.tmpDir);
  const azuriteDataDir = path.join(tmpDir, "azurite");
  const cosmosKeyFile = path.join(tmpDir, "cosmos.key");
  const internalCosmosEndpoint = "http://cosmosdb:8081";
  const internalBlobEndpoint = "http://azurite:10000/devstoreaccount1";
  const publicBlobEndpoint = `http://${config.publicHost}:${config.azuriteBlobPort}/devstoreaccount1`;
  const appBaseUrl = `http://${config.publicHost}:${config.frontendPort}`;
  const azureWebJobsStorage = [
    "DefaultEndpointsProtocol=http",
    "AccountName=devstoreaccount1",
    `AccountKey=${AZURITE_ACCOUNT_KEY}`,
    `BlobEndpoint=${internalBlobEndpoint};`,
  ].join(";");

  return {
    mode: config.mode,
    publicHost: config.publicHost,
    composeFile: path.join(rootDir, config.composeFile),
    composeProjectName: config.composeProjectName,
    tmpDir,
    azuriteDataDir,
    cosmosKeyFile,
    cosmosDatabase: config.cosmosDatabase,
    ports: {
      functions: config.functionsPort,
      frontend: config.frontendPort,
      cosmos: config.cosmosPort,
      cosmosExplorer: config.cosmosExplorerPort,
      azuriteBlob: config.azuriteBlobPort,
    },
    env: {
      TEST_MODE: config.testMode ? "true" : undefined,
      COSMOS_ENDPOINT: internalCosmosEndpoint,
      COSMOS_KEY: COSMOS_KEY,
      COSMOS_DATABASE: config.cosmosDatabase,
      AzureWebJobsStorage: azureWebJobsStorage,
      BLOB_STORAGE_URL: internalBlobEndpoint,
      PUBLIC_BLOB_STORAGE_URL: publicBlobEndpoint,
      APP_BASE_URL: appBaseUrl,
      COOKIE_DOMAIN: config.publicHost,
      BATTLE_NET_COOKIE_SECURE: "false",
      BATTLE_NET_REDIRECT_URI: `http://${config.publicHost}:${config.functionsPort}/api/battlenet/callback`,
    },
  };
}

function buildTestEnvironment(profile, scenario) {
  return {
    ...process.env,
    ...profile.env,
    TEST_MODE: "true",
    BATTLE_NET_REGION: process.env.BATTLE_NET_REGION || "eu",
    LFM_CLIENT_ID: "",
    LFM_CLIENT_SECRET: "",
    SESSION_ENCRYPTION_KEY: process.env.SESSION_ENCRYPTION_KEY || DEFAULT_SESSION_ENCRYPTION_KEY,
    HMAC_SECRET: process.env.HMAC_SECRET || DEFAULT_HMAC_SECRET,
    KEY_VAULT_URL: process.env.KEY_VAULT_URL || "",
    E2E_SCENARIO: scenario,
  };
}

async function buildServeEnvironment(profile) {
  const envFile = await loadEnvFile(path.join(ROOT_DIR, ".env"));
  const merged = {
    ...envFile,
    ...process.env,
  };

  for (const key of [
    "LFM_CLIENT_ID",
    "LFM_CLIENT_SECRET",
    "SESSION_ENCRYPTION_KEY",
    "HMAC_SECRET",
  ]) {
    if (!merged[key]) {
      throw new Error(`Missing required local dev environment variable: ${key}`);
    }
  }

  return {
    ...merged,
    ...profile.env,
    TEST_MODE: "false",
    BATTLE_NET_REGION: merged.BATTLE_NET_REGION || "eu",
    LFM_CLIENT_ID: merged.LFM_CLIENT_ID,
    LFM_CLIENT_SECRET: merged.LFM_CLIENT_SECRET,
    SESSION_ENCRYPTION_KEY: merged.SESSION_ENCRYPTION_KEY,
    HMAC_SECRET: merged.HMAC_SECRET,
    KEY_VAULT_URL: merged.KEY_VAULT_URL || "",
  };
}

function buildMaintenanceEnvironment(profile) {
  return {
    ...process.env,
    ...profile.env,
    TEST_MODE: profile.mode === "test" ? "true" : "false",
    BATTLE_NET_REGION: process.env.BATTLE_NET_REGION || "eu",
    SESSION_ENCRYPTION_KEY: process.env.SESSION_ENCRYPTION_KEY || DEFAULT_SESSION_ENCRYPTION_KEY,
    HMAC_SECRET: process.env.HMAC_SECRET || DEFAULT_HMAC_SECRET,
    LFM_CLIENT_ID: process.env.LFM_CLIENT_ID || "",
    LFM_CLIENT_SECRET: process.env.LFM_CLIENT_SECRET || "",
    KEY_VAULT_URL: process.env.KEY_VAULT_URL || "",
  };
}

function buildComposeEnvironment(profile, runtimeEnv) {
  return {
    ...runtimeEnv,
    TMP_DIR: profile.tmpDir,
    AZURITE_DATA_DIR: profile.azuriteDataDir,
    COSMOS_KEY_FILE: profile.cosmosKeyFile,
    COSMOS_KEY_CONTENT: runtimeEnv.COSMOS_KEY || COSMOS_KEY,
    FUNCTIONS_PORT: String(profile.ports.functions),
    COSMOS_PORT: String(profile.ports.cosmos),
    COSMOS_EXPLORER_PORT: String(profile.ports.cosmosExplorer),
    AZURITE_BLOB_PORT: String(profile.ports.azuriteBlob),
  };
}

export function buildDockerCommandEnvironment(baseEnv) {
  const dockerEnv = { ...baseEnv };
  const dockerContext = baseEnv.LFM_DOCKER_CONTEXT || "default";

  delete dockerEnv.DOCKER_HOST;
  delete dockerEnv.DOCKER_CERT_PATH;
  delete dockerEnv.DOCKER_TLS_VERIFY;
  delete dockerEnv.LFM_DOCKER_CONTEXT;

  dockerEnv.DOCKER_CONTEXT = dockerContext;
  return dockerEnv;
}

function buildFrontendServeEnvironment(profile) {
  return {
    ...process.env,
    FRONTEND_PORT: String(profile.ports.frontend),
    VITE_PROXY_TARGET: `http://127.0.0.1:${profile.ports.functions}`,
    VITE_API_BASE_URL: "/api",
  };
}

function buildFrontendTestEnvironment(profile, scenario = "default") {
  return {
    ...process.env,
    FRONTEND_PORT: String(profile.ports.frontend),
    PLAYWRIGHT_BASE_URL: profile.env.APP_BASE_URL,
    PLAYWRIGHT_BROWSERS_PATH,
    PLAYWRIGHT_INCLUDE_SCENARIO_SPECS: scenario === "default" ? "" : "1",
    VITE_PROXY_TARGET: `http://127.0.0.1:${profile.ports.functions}`,
    VITE_API_BASE_URL: "/api",
  };
}

async function prepareProfile(profile, options = {}) {
  await fs.mkdir(profile.azuriteDataDir, { recursive: true });
  if (options.resetAzurite) {
    await emptyDirectory(profile.azuriteDataDir);
  }

  await fs.writeFile(profile.cosmosKeyFile, COSMOS_KEY);
}

async function emptyDirectory(directoryPath) {
  if (!existsSync(directoryPath)) return;

  for (const entry of await fs.readdir(directoryPath)) {
    await fs.rm(path.join(directoryPath, entry), { recursive: true, force: true });
  }
}

async function ensureDependencies(profile, composeEnv) {
  await runDockerCompose(profile, composeEnv, ["up", "-d", "cosmosdb", "azurite"]);
  await waitForPort("127.0.0.1", profile.ports.cosmos);
  await waitForPort("127.0.0.1", profile.ports.azuriteBlob);
}

async function buildFunctionsImage(profile, composeEnv) {
  await runDockerCompose(profile, composeEnv, ["build", "functions"]);
}

async function startFunctionsService(profile, composeEnv) {
  await runDockerCompose(profile, composeEnv, ["up", "-d", "functions"]);
  await waitForHttp(`http://127.0.0.1:${profile.ports.functions}/api/health`);
}

export function isRetryableFunctionsScriptOutput(output) {
  return (
    output.includes("pgcosmos extension is still starting; retry request shortly") ||
    (output.includes('"statusCode": 500') && output.includes("Azurite-Blob/")) ||
    (output.includes("RestError: connect ECONNREFUSED") && output.includes('"url": "http://cosmosdb:8081"'))
  );
}

export async function retryCommandResult(
  runAttempt,
  {
    attempts = 1,
    delayMs = 0,
    sleepFn = sleep,
    shouldRetry = () => false,
  } = {}
) {
  let lastResult = null;

  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    lastResult = await runAttempt(attempt);
    if (lastResult.exitCode === 0) {
      return lastResult;
    }

    if (attempt === attempts || !shouldRetry(lastResult)) {
      return lastResult;
    }

    await sleepFn(delayMs);
  }

  return lastResult;
}

async function runFunctionsScript(profile, composeEnv, scriptName, scriptArgs = []) {
  const composeArgs = [
    "run",
    "--rm",
    "--entrypoint",
    "node",
    "functions",
    `dist/src/scripts/${scriptName}`,
    ...scriptArgs,
  ];
  const result = await retryCommandResult(
    () => runDockerComposeCapture(profile, composeEnv, composeArgs),
    {
      attempts: 12,
      delayMs: 5000,
      shouldRetry: (result) => isRetryableFunctionsScriptOutput(`${result.stdout}\n${result.stderr}`),
    }
  );

  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);

  if (result.exitCode !== 0) {
    throw new Error(`docker compose ${dockerComposeArgs(profile, composeArgs).join(" ")} exited with code ${result.exitCode}`);
  }
}

async function ensurePlaywrightBrowser() {
  if (await hasCachedChromium()) return;

  await runCommand("npx", ["playwright", "install", "chromium"], {
    cwd: FRONTEND_DIR,
    env: {
      ...process.env,
      PLAYWRIGHT_BROWSERS_PATH,
    },
    stdio: "inherit",
  });
}

async function hasCachedChromium() {
  try {
    const entries = await fs.readdir(PLAYWRIGHT_BROWSERS_PATH);
    return entries.some((entry) => entry.startsWith("chromium-"));
  } catch {
    return false;
  }
}

function createShutdownTracker() {
  let child = null;
  let interrupted = false;

  const handler = () => {
    interrupted = true;
    if (child) {
      child.kill("SIGINT");
    }
  };

  process.on("SIGINT", handler);
  process.on("SIGTERM", handler);

  return {
    attachChild(nextChild) {
      child = nextChild;
    },
    wasInterrupted() {
      return interrupted;
    },
    throwIfInterrupted() {
      if (interrupted) {
        throw new Error("Interrupted");
      }
    },
    dispose() {
      process.off("SIGINT", handler);
      process.off("SIGTERM", handler);
    },
  };
}

function spawnCommand(command, args, options) {
  return spawn(command, args, {
    cwd: options.cwd,
    env: options.env,
    stdio: options.stdio ?? "inherit",
  });
}

async function runCommand(command, args, options) {
  const child = spawnCommand(command, args, options);
  const exitCode = await waitForChild(child);
  if (exitCode !== 0) {
    throw new Error(`${command} ${args.join(" ")} exited with code ${exitCode}`);
  }
}

async function runCommandCapture(command, args, options) {
  const child = spawnCommand(command, args, {
    cwd: options.cwd,
    env: options.env,
    stdio: "pipe",
  });
  const stdoutChunks = [];
  const stderrChunks = [];

  child.stdout?.on("data", (chunk) => {
    stdoutChunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  });
  child.stderr?.on("data", (chunk) => {
    stderrChunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  });

  const exitCode = await waitForChild(child);
  return {
    exitCode,
    stdout: Buffer.concat(stdoutChunks).toString("utf8"),
    stderr: Buffer.concat(stderrChunks).toString("utf8"),
  };
}

async function waitForChild(child) {
  return new Promise((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", (exitCode) => {
      resolve(exitCode ?? 0);
    });
  });
}

async function terminateChild(child) {
  if (!child || child.exitCode !== null || child.signalCode) return;

  child.kill("SIGINT");
  const exited = await Promise.race([
    once(child, "exit").then(() => true),
    sleep(5000).then(() => false),
  ]);

  if (!exited) {
    child.kill("SIGKILL");
    await once(child, "exit");
  }
}

async function runDockerCompose(profile, composeEnv, args, options = {}) {
  await runCommand("docker", dockerComposeArgs(profile, args), {
    cwd: ROOT_DIR,
    env: buildDockerCommandEnvironment(composeEnv),
    stdio: options.quiet ? "ignore" : "inherit",
  });
}

async function runDockerComposeCapture(profile, composeEnv, args) {
  return runCommandCapture("docker", dockerComposeArgs(profile, args), {
    cwd: ROOT_DIR,
    env: buildDockerCommandEnvironment(composeEnv),
  });
}

async function stopComposeServices(profile, composeEnv, options = {}) {
  await runDockerCompose(profile, composeEnv, ["stop"], options);
}

async function teardownComposeProject(profile, composeEnv, options = {}) {
  await runDockerCompose(profile, composeEnv, ["down", "--remove-orphans", "--volumes"], options);
}

function dockerComposeArgs(profile, args) {
  return [
    "compose",
    "-p",
    profile.composeProjectName,
    "-f",
    profile.composeFile,
    ...args,
  ];
}

async function waitForPort(host, port, attempts = 120) {
  for (let i = 0; i < attempts; i += 1) {
    if (await canConnect(host, port)) {
      return;
    }

    await sleep(1000);
  }

  throw new Error(`Timed out waiting for ${host}:${port}`);
}

function canConnect(host, port) {
  return new Promise((resolve) => {
    const socket = net.createConnection({ host, port });

    socket.once("connect", () => {
      socket.end();
      resolve(true);
    });
    socket.once("error", () => {
      resolve(false);
    });
  });
}

async function waitForHttp(url, attempts = 120) {
  for (let i = 0; i < attempts; i += 1) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // retry
    }

    await sleep(1000);
  }

  throw new Error(`Timed out waiting for ${url}`);
}

async function loadEnvFile(filePath) {
  try {
    const raw = await fs.readFile(filePath, "utf8");
    const result = {};

    for (const line of raw.split(/\r?\n/u)) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith("#")) continue;

      const separatorIndex = trimmed.indexOf("=");
      if (separatorIndex === -1) continue;

      const key = trimmed.slice(0, separatorIndex).trim();
      const value = trimmed.slice(separatorIndex + 1).trim();
      result[key] = unquote(value);
    }

    return result;
  } catch (error) {
    if (error instanceof Error && "code" in error && error.code === "ENOENT") {
      return {};
    }

    throw error;
  }
}

function unquote(value) {
  if (
    (value.startsWith("\"") && value.endsWith("\""))
    || (value.startsWith("'") && value.endsWith("'"))
  ) {
    return value.slice(1, -1);
  }

  return value;
}

function normalizePlaywrightArgs(args) {
  if (args.length === 0) return [];

  const [first, ...rest] = args;
  if (!first.startsWith("-") && !first.includes("/") && !first.endsWith(".spec.ts")) {
    return [`e2e/${first}.spec.ts`, ...rest];
  }

  return args;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function usage() {
  console.error("Usage: scripts/dev-env.mjs [serve|test|refresh-reference|reset|down]");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : error);
    process.exit(1);
  });
}
