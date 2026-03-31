import fs from "node:fs";

const ENTRY_BUDGET_BYTES = 95_000;
// Covers all modulepreloaded chunks (vendor-react, vendor-mui, entry).
// Higher than the pre-split budget because vendor chunks that previously
// loaded as separate auto-split chunks are now modulepreloaded.
const INITIAL_JS_BUDGET_BYTES = 780_000;

const html = fs.readFileSync(new URL("../dist/index.html", import.meta.url), "utf8");
const entryChunkMatch = html.match(/<script[^>]+src="\/?(assets\/[^"' ]+\.js)"/);
const jsFiles = [...new Set([...html.matchAll(/assets\/[^"' ]+\.js/g)].map(([match]) => match))];
const sizes = jsFiles.map((file) => ({
  file,
  bytes: fs.statSync(new URL(`../dist/${file}`, import.meta.url)).size,
}));

const entryChunk = entryChunkMatch
  ? sizes.find((entry) => entry.file === entryChunkMatch[1])
  : undefined;
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
