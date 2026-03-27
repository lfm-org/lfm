import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";

test("verify-security.sh expects the same storage defaultAction as storage.bicep", () => {
  const bicep = fs.readFileSync(new URL("../modules/storage.bicep", import.meta.url), "utf8");
  const script = fs.readFileSync(new URL("./verify-security.sh", import.meta.url), "utf8");
  const bicepMatch = bicep.match(/defaultAction:\s*'(?<defaultAction>\w+)'/);
  assert.ok(bicepMatch?.groups?.defaultAction, "storage.bicep should declare networkAcls.defaultAction");

  const expectedDefaultAction = bicepMatch.groups.defaultAction;
  const storageCheckMatch = script.match(
    /check "Storage: networkAcls defaultAction is (?<label>\w+)"\s*\\\s*"(?<expected>\w+)"\s*\\\s*"\$\(az storage account show[\s\S]*?--query "networkRuleSet\.defaultAction" --output tsv\)"/,
  );
  assert.ok(storageCheckMatch?.groups, "verify-security.sh should check storage networkRuleSet.defaultAction");
  assert.equal(storageCheckMatch.groups.label, expectedDefaultAction);
  assert.equal(storageCheckMatch.groups.expected, expectedDefaultAction);
});
