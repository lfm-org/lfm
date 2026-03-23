import { pathToFileURL } from "url";
import { writeBlob } from "../lib/blob.js";
import {
  assertLocalSeedEnvironment,
  buildReferenceDataWrites,
  loadReferenceDataBundle,
  resolveE2eScenario,
  resolveSnapshotDir,
  resolveTestDataTimestamp,
} from "./e2e-test-data.js";

export function assertLocalReferenceDataEnvironment(env: Record<string, string | undefined> = process.env): void {
  try {
    assertLocalSeedEnvironment(env);
  } catch {
    throw new Error("load-test-reference-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");
  }
}

export async function loadTestReferenceData() {
  assertLocalReferenceDataEnvironment();

  const snapshotDir = resolveSnapshotDir();
  const bundle = await loadReferenceDataBundle(snapshotDir);
  const scenario = resolveE2eScenario(process.env.E2E_SCENARIO);
  const writes = buildReferenceDataWrites(
    bundle,
    resolveTestDataTimestamp(process.env.TEST_REFERENCE_TIMESTAMP),
    scenario
  );

  for (const write of writes) {
    await writeBlob(write.blobName, write.data);
  }

  console.log(`Loaded ${writes.length} reference blobs from ${snapshotDir}`);
}

async function main() {
  await loadTestReferenceData();
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error);
    process.exit(1);
  });
}
