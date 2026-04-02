import { Box, Typography } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { useTranslation } from "react-i18next";
import RunInfoCard from "./RunInfoCard";
import RunRosterGrid from "./RunRosterGrid";
import RunSignupCard, { type RunSignupCharacter } from "./RunSignupCard";
import type { Run } from "../lib/runTypes";

interface RunListCardProps {
  run: Run;
  modeLabel: string;
  isMobile: boolean;
  isExpanded: boolean;
  onToggle: () => void;
  onRunUpdate: (run: Run) => void;
  characters: RunSignupCharacter[];
  selectedCharacterId: string | null;
  loadingChars: boolean;
  charactersError: string | null;
  guildTimezone?: string;
  canSignupToGuildRuns: boolean;
  currentBattleNetId?: string | null;
  canDeleteGuildRuns?: boolean;
  canCreateGuildRuns?: boolean;
  onRunDelete?: (runId: string) => void;
  onRunEdit?: (runId: string) => void;
}

function getRoleCounts(run: Run) {
  return run.runCharacters.reduce(
    (counts, signup) => {
      counts[signup.role ?? "DPS"] += 1;
      return counts;
    },
    { TANK: 0, HEALER: 0, DPS: 0 }
  );
}

export default function RunListCard({
  run,
  modeLabel,
  isMobile,
  isExpanded,
  onToggle,
  onRunUpdate,
  characters,
  selectedCharacterId,
  loadingChars,
  charactersError,
  guildTimezone,
  canSignupToGuildRuns,
  currentBattleNetId,
  canDeleteGuildRuns,
  canCreateGuildRuns,
  onRunDelete,
  onRunEdit,
}: RunListCardProps) {
  const { t } = useTranslation();
  const roleCounts = getRoleCounts(run);
  const showDetails = !isMobile || isExpanded;

  return (
    <Box
      component="section"
      aria-label={`${run.instanceName}: ${run.description}`}
      id={`run-card-${run.id}`}
      data-testid="run-card"
      sx={{ display: "grid", gap: 2, border: "1px solid", borderColor: "divider", borderRadius: 2, p: 2 }}
    >
      <RunInfoCard run={run} modeLabel={modeLabel} guildTimezone={guildTimezone} currentBattleNetId={currentBattleNetId} canDeleteGuildRuns={canDeleteGuildRuns} canCreateGuildRuns={canCreateGuildRuns} onRunDelete={onRunDelete} onRunEdit={onRunEdit}>
        {isMobile && (
          <Box
            onClick={onToggle}
            role="button"
            tabIndex={0}
            aria-expanded={isExpanded}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onToggle();
              }
            }}
            sx={{
              mt: 1.5,
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 1,
              cursor: "pointer",
              borderRadius: 1,
              p: 0.5,
              mx: -0.5,
              "&:hover": { bgcolor: "action.hover" },
              "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 2 },
            }}
          >
            <Typography variant="caption" color="text.secondary">
              {[
                t("runList.signups", { count: run.runCharacters.length }),
                t("runList.tanks", { count: roleCounts.TANK }),
                t("runList.healers", { count: roleCounts.HEALER }),
                t("runList.dps", { count: roleCounts.DPS }),
              ].join(" \u00b7 ")}
            </Typography>
            <ExpandMoreIcon
              sx={{
                transform: isExpanded ? "rotate(180deg)" : "rotate(0deg)",
                transition: "transform 0.2s",
                color: "text.secondary",
              }}
            />
          </Box>
        )}
      </RunInfoCard>

      {showDetails && (
        <>
          <RunSignupCard
            run={run}
            onRunUpdate={onRunUpdate}
            characters={characters}
            selectedCharacterId={selectedCharacterId}
            loadingChars={loadingChars}
            charactersError={charactersError}
            guildTimezone={guildTimezone}
            canSignupToGuildRuns={canSignupToGuildRuns}
          />
          <RunRosterGrid signups={run.runCharacters} />
        </>
      )}
    </Box>
  );
}
