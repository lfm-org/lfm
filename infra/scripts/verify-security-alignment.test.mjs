import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";

test("verify-security.sh expects the same storage defaultAction as storage.bicep", () => {
  const bicep = fs.readFileSync(new URL("../modules/storage.bicep", import.meta.url), "utf8");
  const script = fs.readFileSync(new URL("./verify-security.sh", import.meta.url), "utf8");

  assert.match(bicep, /defaultAction:\s*'Allow'/);
  assert.match(script, /Storage: networkAcls defaultAction is Allow/);
  assert.match(script, /"Allow"/);
});
