import fs from "node:fs";

const ENTRY_BUDGET_BYTES = 300_000;
const INITIAL_JS_BUDGET_BYTES = 650_000;

const html = fs.readFileSync(new URL("../dist/index.html", import.meta.url), "utf8");
const jsFiles = [...html.matchAll(/assets\/[^"' ]+\.js/g)].map(([match]) => match);
const sizes = jsFiles.map((file) => ({
  file,
  bytes: fs.statSync(new URL(`../dist/${file}`, import.meta.url)).size,
}));

const entryChunk = sizes[0];
const initialJsBytes = sizes.reduce((sum, entry) => sum + entry.bytes, 0);

if (!entryChunk) {
  console.error("No entry chunk found in dist/index.html");
  process.exit(1);
}

if (entryChunk.bytes > ENTRY_BUDGET_BYTES || initialJsBytes > INITIAL_JS_BUDGET_BYTES) {
  console.error(`Bundle budget failed: entry=${entryChunk.bytes} initial=${initialJsBytes}`);
  process.exit(1);
}

console.log(`Bundle budget passed: entry=${entryChunk.bytes} initial=${initialJsBytes}`);
