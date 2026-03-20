import { defineConfig, devices } from "@playwright/test";

const frontendPort = process.env.FRONTEND_PORT || "4173";
const baseURL = process.env.PLAYWRIGHT_BASE_URL || `http://127.0.0.1:${frontendPort}`;

// Specs that require a non-default E2E_SCENARIO (set up by e2e-all.sh).
// Excluded from open test discovery so `e2e.sh` (default scenario) doesn't
// pick them up and fail; e2e-all.sh passes them explicitly by filename.
const SCENARIO_SPECS = [
  "**/raids-empty.spec.ts",
  "**/raids-error.spec.ts",
  "**/characters-empty.spec.ts",
  "**/create-raid-instances-missing.spec.ts",
];

export default defineConfig({
  testDir: "./e2e",
  testIgnore: SCENARIO_SPECS,
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
