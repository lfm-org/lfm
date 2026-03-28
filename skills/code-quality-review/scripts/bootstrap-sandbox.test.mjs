import test from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptPath = fileURLToPath(new URL("./bootstrap-sandbox.sh", import.meta.url));

function createTempRepo(files = []) {
  const repoDir = mkdtempSync(path.join(tmpdir(), "code-quality-review-"));

  for (const relativePath of files) {
    const fullPath = path.join(repoDir, relativePath);
    mkdirSync(path.dirname(fullPath), { recursive: true });
    writeFileSync(fullPath, "\n", "utf8");
  }

  return repoDir;
}

function readPlan(repoDir) {
  try {
    const stdout = execFileSync("bash", [scriptPath, "--json", "--repo", repoDir], {
      encoding: "utf8",
    });
    return JSON.parse(stdout);
  } catch (error) {
    // Claude Code's sandbox may throw EPERM even when the script exits 0.
    // The stdout still contains valid output in this case.
    if (error?.code === "EPERM" && error?.status === 0 && typeof error?.stdout === "string" && error.stdout) {
      return JSON.parse(error.stdout);
    }
    throw error;
  }
}

test("bootstrap sandbox plan includes the pinned base toolkit even without language manifests", () => {
  const repoDir = createTempRepo();

  try {
    const plan = readPlan(repoDir);

    assert.equal(plan.repo, repoDir);
    assert.deepEqual(plan.fallbackOrder, ["docker-sandbox", "docker-images", "local-install"]);
    assert.deepEqual(plan.baseTools.map((tool) => tool.id), [
      "git",
      "jq",
      "ripgrep",
      "scc",
      "lizard",
      "semgrep",
      "trivy",
      "hyperfine",
      "jscpd",
    ]);
    assert.deepEqual(plan.adapters, []);
  } finally {
    rmSync(repoDir, { recursive: true, force: true });
  }
});

test("bootstrap sandbox plan detects the wider backend adapter packs from repository manifests", () => {
  const repoDir = createTempRepo([
    "package.json",
    "pyproject.toml",
    "go.mod",
    "Cargo.toml",
    "pom.xml",
    "Example.csproj",
    "Gemfile",
    "composer.json",
  ]);

  try {
    const plan = readPlan(repoDir);

    assert.deepEqual(plan.adapters.map((adapter) => adapter.id), [
      "javascript-typescript",
      "python",
      "go",
      "rust",
      "java-kotlin",
      "dotnet",
      "ruby",
      "php",
    ]);

    assert.deepEqual(
      Object.fromEntries(plan.adapters.map((adapter) => [adapter.id, adapter.reasons])),
      {
        "javascript-typescript": ["package.json"],
        python: ["pyproject.toml"],
        go: ["go.mod"],
        rust: ["Cargo.toml"],
        "java-kotlin": ["pom.xml"],
        dotnet: ["Example.csproj"],
        ruby: ["Gemfile"],
        php: ["composer.json"],
      }
    );
  } finally {
    rmSync(repoDir, { recursive: true, force: true });
  }
});
