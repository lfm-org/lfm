#!/usr/bin/env bash
# Verifies that deployed Azure resources match the expected security posture.
# Run after deploying infra to confirm all OWASP findings are resolved.
#
# Usage:
#   RG=lfm \
#   STORAGE_ACCOUNT=lfmstore \
#   COSMOS_ACCOUNT=lfm-cosmos \
#   KV_NAME=lfm-kv \
#   FUNC_APP=lfm-functions \
#   SUB=$(az account show --query id -o tsv) \
#   ./infra/scripts/verify-security.sh
#
# Prerequisites: az CLI, authenticated (az login or workload identity)

set -euo pipefail

: "${RG:?RG (resource group name) must be set}"
: "${STORAGE_ACCOUNT:?STORAGE_ACCOUNT must be set}"
: "${COSMOS_ACCOUNT:?COSMOS_ACCOUNT must be set}"
: "${KV_NAME:?KV_NAME must be set}"
: "${FUNC_APP:?FUNC_APP must be set}"
: "${SUB:?SUB (subscription ID) must be set}"

PASS=0
FAIL=0

check() {
  local desc="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    echo "  PASS  $desc"
    PASS=$((PASS + 1))
  else
    echo "  FAIL  $desc — expected '$expected', got '$actual'"
    FAIL=$((FAIL + 1))
  fi
}

echo ""
echo "=== Finding 1: Anonymous blob access ==="

check "Storage: allowBlobPublicAccess is false" \
  "false" \
  "$(az storage account show --name "$STORAGE_ACCOUNT" --resource-group "$RG" \
      --query "allowBlobPublicAccess" --output tsv)"

check "Storage: wow container publicAccess is None" \
  "None" \
  "$(az storage container show --name wow --account-name "$STORAGE_ACCOUNT" \
      --auth-mode login \
      --query "properties.publicAccess" --output tsv 2>/dev/null || echo 'None')"

echo ""
echo "=== Finding 2: Network restrictions ==="

check "Storage: networkAcls defaultAction is Deny" \
  "Deny" \
  "$(az storage account show --name "$STORAGE_ACCOUNT" --resource-group "$RG" \
      --query "networkRuleSet.defaultAction" --output tsv)"

check "Storage: networkAcls bypass includes AzureServices" \
  "AzureServices" \
  "$(az storage account show --name "$STORAGE_ACCOUNT" --resource-group "$RG" \
      --query "networkRuleSet.bypass[0]" --output tsv)"

check "Key Vault: networkAcls defaultAction is Deny" \
  "Deny" \
  "$(az keyvault show --name "$KV_NAME" --resource-group "$RG" \
      --query "properties.networkAcls.defaultAction" --output tsv)"

echo ""
echo "=== Finding 3: Publishing credentials ==="

SCM_ALLOW=$(az rest \
  --method get \
  --url "https://management.azure.com/subscriptions/${SUB}/resourceGroups/${RG}/providers/Microsoft.Web/sites/${FUNC_APP}/basicPublishingCredentialsPolicies/scm?api-version=2023-12-01" \
  --query "properties.allow" --output tsv)
check "Function App: SCM publishing credentials disabled" "false" "$SCM_ALLOW"

FTP_ALLOW=$(az rest \
  --method get \
  --url "https://management.azure.com/subscriptions/${SUB}/resourceGroups/${RG}/providers/Microsoft.Web/sites/${FUNC_APP}/basicPublishingCredentialsPolicies/ftp?api-version=2023-12-01" \
  --query "properties.allow" --output tsv)
check "Function App: FTP publishing credentials disabled" "false" "$FTP_ALLOW"

echo ""
echo "=== Finding 4: Cosmos DB local auth ==="

check "Cosmos DB: disableLocalAuth is true" \
  "true" \
  "$(az cosmosdb show --name "$COSMOS_ACCOUNT" --resource-group "$RG" \
      --query "disableLocalAuth" --output tsv)"

check "Cosmos DB: disableKeyBasedMetadataWriteAccess is true" \
  "true" \
  "$(az cosmosdb show --name "$COSMOS_ACCOUNT" --resource-group "$RG" \
      --query "disableKeyBasedMetadataWriteAccess" --output tsv)"

echo ""
echo "=== Finding 5: Diagnostic settings ==="

KV_DIAG_COUNT=$(az monitor diagnostic-settings list \
  --resource "/subscriptions/${SUB}/resourceGroups/${RG}/providers/Microsoft.KeyVault/vaults/${KV_NAME}" \
  --query "length(@)" --output tsv)
check "Key Vault: diagnostic settings exist" "1" "$KV_DIAG_COUNT"

FUNC_DIAG_COUNT=$(az monitor diagnostic-settings list \
  --resource "/subscriptions/${SUB}/resourceGroups/${RG}/providers/Microsoft.Web/sites/${FUNC_APP}" \
  --query "length(@)" --output tsv)
check "Function App: diagnostic settings exist" "1" "$FUNC_DIAG_COUNT"

COSMOS_DIAG_COUNT=$(az monitor diagnostic-settings list \
  --resource "/subscriptions/${SUB}/resourceGroups/${RG}/providers/Microsoft.DocumentDB/databaseAccounts/${COSMOS_ACCOUNT}" \
  --query "length(@)" --output tsv)
check "Cosmos DB: diagnostic settings exist" "1" "$COSMOS_DIAG_COUNT"

echo ""
echo "=== Finding 6: Key Vault lifecycle ==="

check "Key Vault: purge protection enabled" \
  "true" \
  "$(az keyvault show --name "$KV_NAME" --resource-group "$RG" \
      --query "properties.enablePurgeProtection" --output tsv)"

check "Key Vault: softDeleteRetentionInDays is 90" \
  "90" \
  "$(az keyvault show --name "$KV_NAME" --resource-group "$RG" \
      --query "properties.softDeleteRetentionInDays" --output tsv)"

echo ""
echo "=== Summary ==="
echo "  Passed: $PASS"
echo "  Failed: $FAIL"
echo ""

if [ "$FAIL" -gt 0 ]; then
  exit 1
fi
