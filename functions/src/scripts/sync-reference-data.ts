import { pathToFileURL } from "url";
import { syncBlizzardReferenceData } from "../lib/reference-sync-blizzard.js";

async function main() {
  const force = process.argv.includes("--force");
  const result = await syncBlizzardReferenceData({
    force,
    log: (message) => console.log(message),
  });

  console.log(JSON.stringify(result, null, 2));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error);
    process.exit(1);
  });
}
