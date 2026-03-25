import { defineConfig, devices } from "@playwright/test";

const frontendPort = process.env.FRONTEND_PORT || "4173";
const baseURL = process.env.PLAYWRIGHT_BASE_URL || `http://127.0.0.1:${frontendPort}`;
const includeScenarioSpecs = process.env.PLAYWRIGHT_INCLUDE_SCENARIO_SPECS === "1";
const includePerfSpecs = process.env.PLAYWRIGHT_INCLUDE_PERF_SPECS === "1";

// Specs that require a non-default E2E_SCENARIO (set up by e2e-all.sh).
// Excluded from open test discovery so `e2e.sh` (default scenario) doesn't
// pick them up and fail; e2e-all.sh passes them explicitly by filename.
const SCENARIO_SPECS = [
  "**/raids-empty.spec.ts",
  "**/raids-error.spec.ts",
  "**/characters-empty.spec.ts",
  "**/create-raid-instances-missing.spec.ts",
];

// Perf specs are always excluded from default discovery. Run them explicitly
// with PLAYWRIGHT_INCLUDE_PERF_SPECS=1 — they need the Docker stack running
// but should not gate normal E2E runs until baselines are established.
const PERF_SPECS = ["**/perf/**"];

export default defineConfig({
  testDir: "./e2e",
  // The local Docker stack uses a shared seeded database, so default E2E runs
  // must stay single-worker until scenarios are fully isolated per test.
  workers: 1,
  testIgnore: [
    ...(includePerfSpecs ? [] : PERF_SPECS),
    ...(includeScenarioSpecs ? [] : SCENARIO_SPECS),
  ],
  outputDir: "./e2e/test-results/artifacts",
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "./e2e/test-results/playwright-report" }],
  ],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: {
    command: "npm run dev:e2e",
    url: baseURL,
    reuseExistingServer: false,
    stdout: "pipe",
    stderr: "pipe",
    timeout: 120000,
  },
  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
      },
    },
  ],
});
