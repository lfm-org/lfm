#!/usr/bin/env node
/**
 * Codebase hygiene checks that knip does not cover:
 *   1. Unused i18n translation keys
 *   2. Stale environment variable references
 *   3. Unused Azure Functions route handlers
 *   4. Dead Bicep parameters/variables
 *
 * Outputs JSON to stdout. Exit 0 = no findings, exit 1 = findings exist.
 */

import { readFileSync, readdirSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { join, resolve } from "node:path";

// Resolve project root via git (script lives in .claude/skills/validate-hygiene/scripts/)
const ROOT = execFileSync("git", ["rev-parse", "--show-toplevel"], {
  encoding: "utf8",
}).trim();

// ── helpers ──────────────────────────────────────────────────────────────────

/** Run grep safely via execFileSync (no shell injection). Returns match output or "". */
function grep(pattern, path, flags = ["-r", "--include=*.ts", "--include=*.tsx", "-l"]) {
  try {
    return execFileSync("grep", [...flags, pattern, path], {
      encoding: "utf8",
      stdio: ["pipe", "pipe", "pipe"],
    }).trim();
  } catch {
    return "";
  }
}

/** Extract matching strings from files via grep -oh. */
function grepExtract(pattern, path, extraIncludes = []) {
  const includes = ["--include=*.ts", "--include=*.tsx", ...extraIncludes];
  try {
    return execFileSync(
      "grep",
      ["-roh", "-E", ...includes, pattern, path],
      { encoding: "utf8", stdio: ["pipe", "pipe", "pipe"] }
    ).trim();
  } catch {
    return "";
  }
}

function readJson(filePath) {
  return JSON.parse(readFileSync(filePath, "utf8"));
}

// ── Check 1: Unused i18n keys ───────────────────────────────────────────────

function checkI18nKeys() {
  const findings = [];
  const localeFile = join(ROOT, "frontend/src/i18n/locales/en.json");
  const locale = readJson(localeFile);
  const keys = Object.keys(locale);
  const srcDir = join(ROOT, "frontend/src");

  // Read all source files once to check for dynamic key patterns
  const allSource = execFileSync("grep", ["-roh", "-E", "t\\(['\"`][^'\"` ]+", srcDir,
    "--include=*.ts", "--include=*.tsx"], {
    encoding: "utf8", stdio: ["pipe", "pipe", "pipe"],
  }).trim();

  // Build set of literal keys found in t() calls
  const literalKeys = new Set(
    allSource.split("\n").filter(Boolean).map((m) => m.replace(/^t\(['"`]/, ""))
  );

  // Detect dynamic key patterns: t(`prefix.${var}.suffix`) -> extract prefix and suffix
  const dynamicPatterns = [];
  const dynamicHits = grepExtract("t\\(`[^`]+\\$\\{[^}]+\\}[^`]*`\\)", srcDir);
  for (const hit of dynamicHits.split("\n").filter(Boolean)) {
    // e.g. t(`landing.valueProps.${item}.title`) -> regex: ^landing\.valueProps\..*\.title$
    const inner = hit.replace(/^t\(`/, "").replace(/`\)$/, "");
    const regexStr = "^" + inner.replace(/\$\{[^}]+\}/g, "[^.]+") + "$";
    try {
      dynamicPatterns.push(new RegExp(regexStr));
    } catch { /* skip malformed */ }
  }

  for (const key of keys) {
    // Check literal match
    if (literalKeys.has(key)) continue;
    // Check grep (catches partial matches, concatenation, etc.)
    if (grep(key, srcDir)) continue;
    // Check dynamic patterns
    if (dynamicPatterns.some((re) => re.test(key))) continue;

    findings.push({ key, file: "frontend/src/i18n/locales/en.json" });
  }
  return findings;
}

// ── Check 2: Stale environment variables ────────────────────────────────────

function checkEnvVars() {
  const findings = { undocumented: [], orphaned: [] };

  const functionsDir = join(ROOT, "functions/src");
  const frontendSrcDir = join(ROOT, "frontend/src");
  const viteConfig = join(ROOT, "frontend/vite.config.ts");

  // Match process.env.VAR patterns (always UPPER_SNAKE or mixed like AzureWebJobsStorage)
  const processEnvHits = grepExtract("process\\.env\\.[A-Za-z_][A-Za-z0-9_]*", functionsDir);
  // Match env.VAR where env is a destructured/parameter reference to process.env
  // Only match names starting with uppercase to avoid false positives like env.endpoint
  const envDotHits = grepExtract("env\\.[A-Z][A-Za-z0-9_]*", functionsDir);
  const importMetaHits = grepExtract("import\\.meta\\.env\\.[A-Z_][A-Z0-9_]*", frontendSrcDir);
  // Also check vite.config.ts for project env vars (skip playwright.config.ts — it reads CI vars)
  const viteConfigHits = existsSync(viteConfig)
    ? grepExtract("process\\.env\\.[A-Z_][A-Z0-9_]*", viteConfig, [])
    : "";

  const extractVarNames = (raw, prefix) =>
    [...new Set(raw.split("\n").filter(Boolean).map((m) => m.replace(prefix, "")))];

  const backendVars = new Set([
    ...extractVarNames(processEnvHits, "process.env."),
    ...extractVarNames(envDotHits, "env."),
  ]);
  const frontendVars = new Set([
    ...extractVarNames(importMetaHits, "import.meta.env."),
    ...extractVarNames(viteConfigHits, "process.env."),
  ]);

  // Built-in vars to ignore
  const builtIn = new Set(["NODE_ENV", "MODE", "DEV", "PROD", "SSR", "BASE_URL"]);
  for (const v of builtIn) {
    backendVars.delete(v);
    frontendVars.delete(v);
  }

  // Read example.env files
  const parseEnvFile = (filePath) =>
    new Set(
      readFileSync(filePath, "utf8")
        .split("\n")
        .map((l) => l.trim())
        .filter((l) => l && !l.startsWith("#"))
        .map((l) => l.split("=")[0])
    );

  const rootEnvVars = parseEnvFile(join(ROOT, "example.env"));
  const frontendEnvVars = parseEnvFile(join(ROOT, "frontend/example.env"));

  // Cross-reference backend vars
  for (const v of backendVars) {
    if (!rootEnvVars.has(v)) {
      findings.undocumented.push({ var: v, scope: "functions", expectedIn: "example.env" });
    }
  }
  for (const v of frontendVars) {
    if (!frontendEnvVars.has(v) && !rootEnvVars.has(v)) {
      findings.undocumented.push({ var: v, scope: "frontend", expectedIn: "frontend/example.env" });
    }
  }

  // Check for orphaned vars in example.env
  const allUsedVars = new Set([...backendVars, ...frontendVars]);
  // Also check workflow files for var references
  const workflowDir = join(ROOT, ".github/workflows");
  const workflowHits = grepExtract("[A-Z_][A-Z0-9_]*", workflowDir, [
    "--include=*.yml",
  ]);
  const workflowVars = new Set(workflowHits.split("\n").filter(Boolean));

  for (const v of rootEnvVars) {
    if (!allUsedVars.has(v) && !workflowVars.has(v)) {
      findings.orphaned.push({ var: v, file: "example.env" });
    }
  }
  for (const v of frontendEnvVars) {
    if (!allUsedVars.has(v) && !workflowVars.has(v)) {
      findings.orphaned.push({ var: v, file: "frontend/example.env" });
    }
  }

  return findings;
}

// ── Check 3: Unused Azure Functions routes ──────────────────────────────────

function checkRoutes() {
  const findings = [];
  const functionsDir = join(ROOT, "functions/src/functions");
  const frontendSrc = join(ROOT, "frontend/src");

  const plannedRoutes = new Set(["raids/{id}:GET", "raids/{id}:PUT", "raids/{id}:DELETE"]);
  // Infrastructure routes: health check, CORS preflight, OAuth callback (redirect, not API call)
  const skipRoutes = new Set(["health", "{*route}", "battlenet/callback"]);

  const files = readdirSync(functionsDir).filter((f) => f.endsWith(".ts"));

  for (const file of files) {
    const content = readFileSync(join(functionsDir, file), "utf8");
    const routeMatch = content.match(/route:\s*["']([^"']+)["']/);
    const methodsMatch = content.match(/methods:\s*\[([^\]]+)\]/);
    const authMatch = content.match(/authLevel:\s*["'](\w+)["']/);

    if (!routeMatch) continue;

    const route = routeMatch[1];
    const methods = methodsMatch
      ? methodsMatch[1].replace(/["'\s]/g, "").split(",")
      : ["GET"];
    const authLevel = authMatch ? authMatch[1] : "anonymous";

    if (authLevel === "function") continue;
    if (skipRoutes.has(route)) continue;

    for (const method of methods) {
      const routeKey = `${route}:${method}`;

      // Search for each static segment of the route in frontend code.
      // e.g. "raids/{id}/signup" -> search for each of ["raids", "signup"]
      const segments = route.split("/").filter((s) => !s.startsWith("{") && s);
      // A route is "used" if ALL its static segments appear in at least one frontend file
      const found = segments.length > 0 && segments.every((seg) => grep(`/${seg}`, frontendSrc));

      if (!found) {
        const planned = plannedRoutes.has(routeKey);
        findings.push({
          route: `${method} /${route}`,
          file: `functions/src/functions/${file}`,
          status: planned ? "planned" : "unused",
        });
      }
    }
  }
  return findings;
}

// ── Check 4: Dead Bicep parameters ──────────────────────────────────────────

function checkBicep() {
  const findings = [];
  const infraDir = join(ROOT, "infra");

  if (!existsSync(infraDir)) return findings;

  const mainBicep = readFileSync(join(infraDir, "main.bicep"), "utf8");
  const mainParams = [...mainBicep.matchAll(/^param\s+(\w+)/gm)].map((m) => m[1]);

  const paramFile = readJson(join(infraDir, "parameters.prod.lfm.json"));
  const paramFileKeys = Object.keys(paramFile.parameters || {});

  for (const p of mainParams) {
    if (!paramFileKeys.includes(p)) {
      findings.push({ type: "missing_from_param_file", param: p, file: "infra/main.bicep" });
    }
  }
  for (const p of paramFileKeys) {
    if (!mainParams.includes(p)) {
      findings.push({ type: "missing_from_main", param: p, file: "infra/parameters.prod.lfm.json" });
    }
  }

  const modulesDir = join(infraDir, "modules");
  if (existsSync(modulesDir)) {
    const modules = readdirSync(modulesDir).filter((f) => f.endsWith(".bicep"));
    for (const mod of modules) {
      const content = readFileSync(join(modulesDir, mod), "utf8");
      const params = [...content.matchAll(/^param\s+(\w+)/gm)].map((m) => m[1]);
      const vars = [...content.matchAll(/^var\s+(\w+)/gm)].map((m) => m[1]);

      for (const p of params) {
        const refs = content.split(p).length - 1;
        if (refs <= 1) {
          findings.push({ type: "unused_module_param", param: p, file: `infra/modules/${mod}` });
        }
      }
      for (const v of vars) {
        const refs = content.split(v).length - 1;
        if (refs <= 1) {
          findings.push({ type: "unused_module_var", variable: v, file: `infra/modules/${mod}` });
        }
      }
    }
  }

  return findings;
}

// ── Run all checks ──────────────────────────────────────────────────────────

const results = {
  i18n: checkI18nKeys(),
  envVars: checkEnvVars(),
  routes: checkRoutes(),
  bicep: checkBicep(),
};

const totalFindings =
  results.i18n.length +
  results.envVars.undocumented.length +
  results.envVars.orphaned.length +
  results.routes.length +
  results.bicep.length;

console.log(JSON.stringify(results, null, 2));
process.exit(totalFindings > 0 ? 1 : 0);
