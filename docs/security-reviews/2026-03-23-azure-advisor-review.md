# Azure Advisor Recommendations Review (2026-03-23)

Review of all Azure Advisor recommendations for the `lfm` resource group.

## High Impact — Skip (require paid tiers)

| Resource | Recommendation | Reason to skip |
|----------|---------------|----------------|
| `lfm-functions-plan` | Use Standard or Premium tier | Significant recurring cost |
| `lfm-functions-plan` | Use zone-supported App Service Plan | Requires paid tier |
| `lfm-functions-plan` | Set minimum instance count to 2 | Requires paid tier |

## Medium Impact — Actionable

### 1. Migrate to Flex Consumption
- **Resource:** `lfm-functions`
- **Deadline:** Linux Consumption retiring **2028-09-30**
- **Why:** Flex Consumption has a free grant and better cold starts. Plenty of time but should be planned.

### 2. Optimize indexing on `raiders` container
- **Resource:** `lfm-cosmos`
- **Detail:** 4,244 indexed properties — likely a wildcard `/*` policy indexing far more than needed.
- **Why:** Narrowing the policy saves RU/s on writes at no cost.

### 3. Add composite indexes on `raids` container
- **Resource:** `lfm-cosmos`
- **Suggested indexes:** `/visibility ASC`, `/startTime ASC`, `/creatorBattleNetId ASC`, `/creatorGuildId ASC`
- **Why:** Matches common query patterns; improves read performance at no cost.

## Medium Impact — Skip

| Resource | Recommendation | Reason to skip |
|----------|---------------|----------------|
| `lfm-cosmos` | Enable continuous backup | Adds cost beyond free tier periodic backup |
| `lfm-cosmos` | Enable resource-specific diagnostic settings | Potential ingestion costs beyond free 5 GB/month |
